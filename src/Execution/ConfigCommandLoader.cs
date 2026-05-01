namespace Rexo.Execution;

using System.Text.Json;
using System.Text.RegularExpressions;
using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;
using Rexo.Versioning;

public sealed class ConfigCommandLoader
{
    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions =
        new() { WriteIndented = true };

    private readonly BuiltinRegistry _builtinRegistry;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly VersionProviderRegistry _versionProviders;
    private readonly Artifacts.ArtifactProviderRegistry _artifactProviders;

    public ConfigCommandLoader(
        BuiltinRegistry builtinRegistry,
        ITemplateRenderer templateRenderer,
        VersionProviderRegistry versionProviders,
        Artifacts.ArtifactProviderRegistry artifactProviders)
    {
        _builtinRegistry = builtinRegistry;
        _templateRenderer = templateRenderer;
        _versionProviders = versionProviders;
        _artifactProviders = artifactProviders;
    }

    public void LoadInto(
        CommandRegistry registry,
        RepoConfig config,
        string repositoryRoot,
        ICommandExecutor commandExecutor)
    {
        RegisterBuiltins(config, repositoryRoot);

        foreach (var (commandName, commandConfig) in config.Commands)
        {
            var name = commandName;
            var cmd = commandConfig;

            registry.Register(name, (invocation, ct) =>
                ExecuteConfigCommandAsync(name, cmd, config, invocation, repositoryRoot, commandExecutor, ct));
        }

        foreach (var (alias, target) in config.Aliases)
        {
            var aliasName = alias;
            var targetName = target;

            registry.Register(aliasName, (invocation, ct) =>
                registry.TryResolve(targetName, out var handler) && handler is not null
                    ? handler(invocation, ct)
                    : Task.FromResult(CommandResult.Fail(aliasName, 8, $"Alias target '{targetName}' not found.")));
        }
    }

    /// <summary>
    /// Loads commands and aliases from a policy definition into the registry.
    /// Policy commands have lower priority than config commands: a policy command is only
    /// registered when no command with the same name already exists.
    /// </summary>
    public void LoadPolicyCommandsInto(
        CommandRegistry registry,
        PolicyConfig policy,
        RepoConfig config,
        string repositoryRoot,
        ICommandExecutor commandExecutor)
    {
        if (policy.Commands is { Count: > 0 })
        {
            foreach (var (commandName, commandConfig) in policy.Commands)
            {
                if (registry.TryResolve(commandName, out _)) continue; // repo.json wins

                var name = commandName;
                var cmd = commandConfig;
                registry.Register(name, (invocation, ct) =>
                    ExecuteConfigCommandAsync(name, cmd, config, invocation, repositoryRoot, commandExecutor, ct));
            }
        }

        if (policy.Aliases is { Count: > 0 })
        {
            foreach (var (alias, target) in policy.Aliases)
            {
                if (registry.TryResolve(alias, out _)) continue; // repo.json wins

                var aliasName = alias;
                var targetName = target;
                registry.Register(aliasName, (invocation, ct) =>
                    registry.TryResolve(targetName, out var handler) && handler is not null
                        ? handler(invocation, ct)
                        : Task.FromResult(CommandResult.Fail(aliasName, 8, $"Alias target '{targetName}' not found.")));
            }
        }
    }

    private void RegisterBuiltins(RepoConfig config, string repositoryRoot)
    {
        // builtin:resolve-version
        _builtinRegistry.Register("builtin:resolve-version", async (step, ctx, ct) =>
        {
            var versioningConfig = config.Versioning is not null
                ? new VersioningConfig(
                    config.Versioning.Provider,
                    config.Versioning.Fallback,
                    config.Versioning.Settings)
                : new VersioningConfig("fixed", "0.1.0-local");

            var provider = _versionProviders.Resolve(versioningConfig.Provider);
            var versionResult = await provider.ResolveAsync(versioningConfig, ctx, ct);

            Console.WriteLine($"  Resolved version: {versionResult.SemVer}");

            return new StepResult(
                step.Id ?? "resolve-version",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["__version"] = versionResult,
                    ["semver"] = versionResult.SemVer,
                    ["major"] = versionResult.Major.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["minor"] = versionResult.Minor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["patch"] = versionResult.Patch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["prerelease"] = versionResult.PreRelease,
                    ["buildMetadata"] = versionResult.BuildMetadata,
                    ["branch"] = versionResult.Branch,
                    ["commitSha"] = versionResult.CommitSha,
                    ["shortSha"] = versionResult.ShortSha,
                    ["assemblyVersion"] = versionResult.AssemblyVersion,
                    ["fileVersion"] = versionResult.FileVersion,
                    ["informationalVersion"] = versionResult.InformationalVersion,
                    ["nugetVersion"] = versionResult.NuGetVersion,
                    ["dockerVersion"] = versionResult.DockerVersion,
                    ["isPrerelease"] = versionResult.IsPreRelease.ToString().ToLowerInvariant(),
                    ["isStable"] = versionResult.IsStable.ToString().ToLowerInvariant(),
                    ["commitsSinceVersionSource"] = versionResult.CommitsSinceVersionSource?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        });

        // builtin:build-artifacts
        _builtinRegistry.Register("builtin:build-artifacts", (step, ctx, ct) =>
            BuildArtifactsAsync(
                step.Id ?? "build-artifacts",
                config,
                ctx,
                includePredicate: static _ => true,
                successMessage: "All artifacts built.",
            emptyMessage: "No artifacts configured.",
            cancellationToken: ct));

        // builtin:tag-artifacts
        _builtinRegistry.Register("builtin:tag-artifacts", (step, ctx, ct) =>
            TagArtifactsAsync(
                step.Id ?? "tag-artifacts",
                config,
                ctx,
                includePredicate: static _ => true,
                successMessage: "All artifacts tagged.",
            emptyMessage: "No artifacts configured.",
            cancellationToken: ct));

        // builtin:push-artifacts
        _builtinRegistry.Register("builtin:push-artifacts", (step, ctx, ct) =>
            PushArtifactsAsync(
                step.Id ?? "push-artifacts",
                config,
                repositoryRoot,
                ctx,
                includePredicate: static _ => true,
                successMessage: "Artifact push phase completed.",
            emptyMessage: "No artifacts configured.",
            cancellationToken: ct));

        // builtin:plan-artifacts
        _builtinRegistry.Register("builtin:plan-artifacts", (step, ctx, ct) =>
            Task.FromResult(PlanArtifacts(
                step.Id ?? "plan-artifacts",
                config,
                includePredicate: static _ => true,
                successMessage: "Planned all artifacts.",
                emptyMessage: "No artifacts configured.")));

        // builtin:ship-artifacts = tag + push
        _builtinRegistry.Register("builtin:ship-artifacts", async (step, ctx, ct) =>
        {
            var tagResult = await TagArtifactsAsync(
                step.Id ?? "ship-artifacts",
                config,
                ctx,
                includePredicate: static _ => true,
                successMessage: "Artifacts tagged.",
                emptyMessage: "No artifacts configured.",
                cancellationToken: ct);

            if (!tagResult.Success)
            {
                return tagResult;
            }

            return await PushArtifactsAsync(
                step.Id ?? "ship-artifacts",
                config,
                repositoryRoot,
                ctx,
                includePredicate: static _ => true,
                successMessage: "Ship completed.",
                emptyMessage: "No artifacts configured.",
                cancellationToken: ct);
        });

        // builtin:all-artifacts = build + tag + push
        _builtinRegistry.Register("builtin:all-artifacts", async (step, ctx, ct) =>
        {
            var buildResult = await BuildArtifactsAsync(
                step.Id ?? "all-artifacts",
                config,
                ctx,
                includePredicate: static _ => true,
                successMessage: "Artifacts built.",
                emptyMessage: "No artifacts configured.",
                cancellationToken: ct);

            if (!buildResult.Success)
            {
                return buildResult;
            }

            var tagResult = await TagArtifactsAsync(
                step.Id ?? "all-artifacts",
                config,
                ctx,
                includePredicate: static _ => true,
                successMessage: "Artifacts tagged.",
                emptyMessage: "No artifacts configured.",
                cancellationToken: ct);

            if (!tagResult.Success)
            {
                return tagResult;
            }

            return await PushArtifactsAsync(
                step.Id ?? "all-artifacts",
                config,
                repositoryRoot,
                ctx,
                includePredicate: static _ => true,
                successMessage: "All workflow completed.",
                emptyMessage: "No artifacts configured.",
                cancellationToken: ct);
        });

        // Dockship-style shorthand aliases, mapped to Rexo artifact workflows.
        _builtinRegistry.Register("builtin:plan", (step, ctx, ct) =>
            Task.FromResult(PlanArtifacts(
                step.Id ?? "plan",
                config,
                includePredicate: static _ => true,
                successMessage: "Planned all artifacts.",
                emptyMessage: "No artifacts configured.")));

        _builtinRegistry.Register("builtin:ship", async (step, ctx, ct) =>
            await (_builtinRegistry.TryResolve("builtin:ship-artifacts", out var ship) && ship is not null
                ? ship(step, ctx, ct)
                : Task.FromResult(new StepResult(step.Id ?? "ship", false, 1, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = "builtin:ship-artifacts is not registered." }))));

        _builtinRegistry.Register("builtin:all", async (step, ctx, ct) =>
            await (_builtinRegistry.TryResolve("builtin:all-artifacts", out var all) && all is not null
                ? all(step, ctx, ct)
                : Task.FromResult(new StepResult(step.Id ?? "all", false, 1, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = "builtin:all-artifacts is not registered." }))));

        // builtin:docker-plan
        _builtinRegistry.Register("builtin:docker-plan", (step, ctx, ct) =>
            Task.FromResult(PlanArtifacts(
                step.Id ?? "docker-plan",
                config,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Planned docker artifacts.",
                emptyMessage: "No docker artifacts configured.")));

        // builtin:docker-ship = tag + push (docker artifacts only)
        _builtinRegistry.Register("builtin:docker-ship", async (step, ctx, ct) =>
        {
            var tagResult = await TagArtifactsAsync(
                step.Id ?? "docker-ship",
                config,
                ctx,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Docker artifacts tagged.",
                emptyMessage: "No docker artifacts configured.",
                cancellationToken: ct);

            if (!tagResult.Success)
            {
                return tagResult;
            }

            return await PushArtifactsAsync(
                step.Id ?? "docker-ship",
                config,
                repositoryRoot,
                ctx,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Docker ship completed.",
                emptyMessage: "No docker artifacts configured.",
                cancellationToken: ct);
        });

        // builtin:docker-all = build + tag + push (docker artifacts only)
        _builtinRegistry.Register("builtin:docker-all", async (step, ctx, ct) =>
        {
            var buildResult = await BuildArtifactsAsync(
                step.Id ?? "docker-all",
                config,
                ctx,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Docker artifacts built.",
                emptyMessage: "No docker artifacts configured.",
                cancellationToken: ct);

            if (!buildResult.Success)
            {
                return buildResult;
            }

            var tagResult = await TagArtifactsAsync(
                step.Id ?? "docker-all",
                config,
                ctx,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Docker artifacts tagged.",
                emptyMessage: "No docker artifacts configured.",
                cancellationToken: ct);

            if (!tagResult.Success)
            {
                return tagResult;
            }

            return await PushArtifactsAsync(
                step.Id ?? "docker-all",
                config,
                repositoryRoot,
                ctx,
                includePredicate: static a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase),
                successMessage: "Docker all completed.",
                emptyMessage: "No docker artifacts configured.",
                cancellationToken: ct);
        });

        // builtin:docker-stage = build one named stage from artifact settings.stages
        _builtinRegistry.Register("builtin:docker-stage", async (step, ctx, ct) =>
        {
            var stageName = ctx.Args.TryGetValue("stage", out var argStage) && !string.IsNullOrWhiteSpace(argStage)
                ? argStage
                : (ctx.Options.TryGetValue("stage", out var optionStage) ? optionStage : null);

            if (string.IsNullOrWhiteSpace(stageName))
            {
                return new StepResult(
                    step.Id ?? "docker-stage",
                    false,
                    2,
                    TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = "Missing stage name. Provide args.stage or --stage." });
            }

            var dockerArtifacts = (config.Artifacts ?? [])
                .Where(a => string.Equals(a.Type, "docker", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dockerArtifacts.Count == 0)
            {
                return new StepResult(
                    step.Id ?? "docker-stage",
                    true,
                    0,
                    TimeSpan.Zero,
                    new Dictionary<string, object?> { ["message"] = "No docker artifacts configured." });
            }

            foreach (var artifactCfg in dockerArtifacts)
            {
                var provider = _artifactProviders.Resolve(artifactCfg.Type);
                if (provider is null)
                {
                    continue;
                }

                if (artifactCfg.Settings is null ||
                    !artifactCfg.Settings.TryGetValue("stages", out var stagesValue) ||
                    stagesValue.ValueKind != JsonValueKind.Object ||
                    !stagesValue.TryGetProperty(stageName, out var selectedStage) ||
                    selectedStage.ValueKind != JsonValueKind.Object)
                {
                    return new StepResult(
                        step.Id ?? "docker-stage",
                        false,
                        2,
                        TimeSpan.Zero,
                        new Dictionary<string, object?>
                        {
                            ["error"] = $"Stage '{stageName}' not found for docker artifact '{artifactCfg.Name}'.",
                        });
                }

                var clonedSettings = CloneSettings(artifactCfg.Settings);
                clonedSettings["stages"] = JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>
                {
                    [stageName] = selectedStage.Clone(),
                });
                clonedSettings["stageFallback"] = JsonSerializer.SerializeToElement(false);

                var artifactConfig = new ArtifactConfig(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    clonedSettings);

                var result = await provider.BuildAsync(artifactConfig, ctx, ct);
                if (!result.Success)
                {
                    return new StepResult(
                        step.Id ?? "docker-stage",
                        false,
                        5,
                        TimeSpan.Zero,
                        new Dictionary<string, object?>
                        {
                            ["error"] = $"Failed to build docker stage '{stageName}' for artifact '{artifactCfg.Name}'.",
                        });
                }
            }

            return new StepResult(
                step.Id ?? "docker-stage",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["message"] = $"Docker stage '{stageName}' completed.",
                });
        });

        // builtin:test
        _builtinRegistry.Register("builtin:test", async (step, ctx, ct) =>
        {
            var testsConfig = config.Tests;
            var verificationConfig = new Verification.VerificationConfig(
                Enabled: testsConfig?.Enabled ?? true,
                Projects: testsConfig?.Projects,
                Configuration: testsConfig?.Configuration ?? "Release",
                ResultsOutput: testsConfig?.ResultsOutput,
                CoverageOutput: testsConfig?.CoverageOutput,
                LineCoverageThreshold: testsConfig?.CoverageThreshold,
                BranchCoverageThreshold: null);

            var result = await Verification.DotnetTestRunner.RunAsync(verificationConfig, repositoryRoot, ct);

            Console.WriteLine($"  Tests: {result.PassedTests}/{result.TotalTests} passed.");
            if (result.FailedTests > 0)
            {
                Console.Error.WriteLine($"  {result.FailedTests} tests failed.");
            }

            return new StepResult(
                step.Id ?? "test",
                result.Success,
                result.Success ? 0 : 4,
                TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["total"] = result.TotalTests.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["passed"] = result.PassedTests.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["failed"] = result.FailedTests.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["skipped"] = result.SkippedTests.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        });

        // builtin:analyze
        _builtinRegistry.Register("builtin:analyze", async (step, ctx, ct) =>
        {
            var analysisResults = new List<Analysis.AnalysisResult>();

            var formatResult = await Analysis.DotnetAnalysisRunner.RunFormatCheckAsync(repositoryRoot, ct);
            analysisResults.Add(formatResult);
            if (!formatResult.Success && (config.Analysis?.FailOnIssues ?? true))
            {
                return new StepResult(step.Id ?? "analyze", false, 1, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = string.Join("; ", formatResult.Issues) });
            }

            // Run custom analysis tools from config.Analysis.Tools
            if (config.Analysis?.Tools is { Length: > 0 })
            {
                foreach (var toolCmd in config.Analysis.Tools)
                {
                    if (string.IsNullOrWhiteSpace(toolCmd)) continue;

                    var toolResult = await Analysis.DotnetAnalysisRunner.RunCustomToolAsync(toolCmd, repositoryRoot, ct);
                    analysisResults.Add(toolResult);

                    if (!toolResult.Success && (config.Analysis.FailOnIssues))
                    {
                        // Write SARIF before returning failure
                        await WriteSarifIfConfiguredAsync(analysisResults, repositoryRoot, config, ct);

                        return new StepResult(step.Id ?? "analyze", false, 1, TimeSpan.Zero,
                            new Dictionary<string, object?> { ["error"] = string.Join("; ", toolResult.Issues) });
                    }
                }
            }

            await WriteSarifIfConfiguredAsync(analysisResults, repositoryRoot, config, ct);

            return new StepResult(step.Id ?? "analyze", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "Analysis passed." });
        });

        // builtin:validate — check config is valid
        _builtinRegistry.Register("builtin:validate", (step, ctx, ct) =>
        {
            Console.WriteLine("  Validating configuration...");
            return Task.FromResult(new StepResult(
                step.Id ?? "validate",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "Configuration is valid." }));
        });

        // builtin:verify = test + analyze
        _builtinRegistry.Register("builtin:verify", async (step, ctx, ct) =>
        {
            Console.WriteLine("  Running verification (test + analyze)...");
            if (_builtinRegistry.TryResolve("builtin:test", out var testBuiltin) && testBuiltin is not null)
            {
                var testResult = await testBuiltin(step, ctx, ct);
                if (!testResult.Success)
                    return testResult with { StepId = step.Id ?? "verify" };
            }

            if (_builtinRegistry.TryResolve("builtin:analyze", out var analyzeBuiltin) && analyzeBuiltin is not null)
            {
                var analyzeResult = await analyzeBuiltin(step, ctx, ct);
                if (!analyzeResult.Success)
                    return analyzeResult with { StepId = step.Id ?? "verify" };
            }

            return new StepResult(step.Id ?? "verify", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "Verification passed." });
        });

        // builtin:config-resolved — serialize current effective config to JSON
        _builtinRegistry.Register("builtin:config-resolved", (step, ctx, ct) =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(config, IndentedJsonOptions);
            Console.WriteLine(json);
            return Task.FromResult(new StepResult(
                step.Id ?? "config-resolved",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?> { ["json"] = json }));
        });

        // builtin:config-materialize — write provider config files (e.g. GitVersion.yml)
        _builtinRegistry.Register("builtin:config-materialize", async (step, ctx, ct) =>
        {
            var materialized = new List<string>();

            // If using gitversion provider, offer to write GitVersion.yml
            if (string.Equals(config.Versioning?.Provider, "gitversion", StringComparison.OrdinalIgnoreCase))
            {
                var gvPath = Path.Combine(repositoryRoot, "GitVersion.yml");
                if (!File.Exists(gvPath))
                {
                    var gvContent = """
                        mode: ContinuousDeployment
                        branches: {}
                        ignore:
                          sha: []
                        """;
                    await File.WriteAllTextAsync(gvPath, gvContent, ct);
                    materialized.Add(gvPath);
                    Console.WriteLine($"  Materialized: {gvPath}");
                }
            }

            var message = materialized.Count > 0
                ? $"Materialized {materialized.Count} file(s)."
                : "Nothing to materialize.";

            return new StepResult(
                step.Id ?? "config-materialize",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["message"] = message,
                    ["files"] = string.Join(", ", materialized),
                });
        });
    }

    private async Task<CommandResult> ExecuteConfigCommandAsync(
        string commandName,
        RepoCommandConfig commandConfig,
        RepoConfig config,
        CommandInvocation invocation,
        string repositoryRoot,
        ICommandExecutor commandExecutor,
        CancellationToken cancellationToken)
    {
        var normalizedCommandConfig = NormalizeCommandConfig(commandConfig);
        var gitInfo = await Git.GitDetector.DetectAsync(repositoryRoot, cancellationToken);
        var ciInfo = CiDetector.Detect();

        var context = new ExecutionContext(
            repositoryRoot,
            gitInfo.Branch,
            gitInfo.CommitSha,
            new Dictionary<string, object?>())
        {
            ShortSha = gitInfo.ShortSha,
            RemoteUrl = gitInfo.RemoteUrl,
            IsCi = ciInfo.IsCi,
            CiProvider = ciInfo.Provider,
            IsPullRequest = ciInfo.IsPullRequest,
            IsCleanWorkingTree = gitInfo.IsClean,
            CiBuildId = ciInfo.BuildId,
            CiRunNumber = ciInfo.RunNumber,
            CiWorkflowName = ciInfo.WorkflowName,
            CiActor = ciInfo.Actor,
            CiTag = ciInfo.Tag,
            CiBuildUrl = ciInfo.BuildUrl,
            Args = invocation.Args,
            Options = BuildOptionsWithDefaults(invocation.Options, normalizedCommandConfig),
            FileEnvironment = RepositoryEnvironmentFiles.Load(repositoryRoot),
        };

        var stepExecutor = new StepExecutor(commandExecutor, _templateRenderer, _builtinRegistry);
        var stepResults = new List<StepResult>();
        var currentContext = context;
        var artifactEntries = new List<Core.Models.ArtifactManifestEntry>();
        var pushDecisionEntries = new List<Core.Models.PushDecision>();

        // Group consecutive parallel steps; sequential steps are singleton groups
        var stepGroups = GroupSteps(normalizedCommandConfig.Steps);

        foreach (var group in stepGroups)
        {
            List<(RepoStepConfig Config, StepResult Result)> executed;

            if (group.Count == 1)
            {
                var stepConfig = group[0];
                var stepDef = BuildStepDefinition(stepConfig);
                var stepResult = await stepExecutor.ExecuteAsync(stepDef, currentContext, cancellationToken);
                executed = [(stepConfig, stepResult)];
            }
            else
            {
                executed = await ExecuteParallelGroupAsync(
                    group,
                    stepExecutor,
                    currentContext,
                    normalizedCommandConfig.MaxParallel,
                    cancellationToken);
            }

            foreach (var (_, stepResult) in executed)
            {
                stepResults.Add(stepResult);
                currentContext = currentContext.WithStep(stepResult);

                if (stepResult.Outputs.TryGetValue("__version", out var versionObj) &&
                    versionObj is VersionResult versionResult)
                {
                    currentContext = currentContext.WithVersion(versionResult);
                }

                if (stepResult.Outputs.TryGetValue("__artifacts", out var artifactsObj) &&
                    artifactsObj is List<Core.Models.ArtifactManifestEntry> stepArtifacts)
                {
                    artifactEntries.AddRange(stepArtifacts);
                }

                if (stepResult.Outputs.TryGetValue("__pushDecisions", out var decisionsObj) &&
                    decisionsObj is List<Core.Models.PushDecision> stepDecisions)
                {
                    pushDecisionEntries.AddRange(stepDecisions);
                }
            }

            // Fail fast if any step failed and it doesn't have continueOnError
            var failed = executed
                .FirstOrDefault(t => !t.Result.Success && t.Config.ContinueOnError != true);

            if (failed.Result is not null)
            {
                return new CommandResult(
                    commandName,
                    false,
                    failed.Result.ExitCode,
                    $"Step '{failed.Result.StepId}' failed with exit code {failed.Result.ExitCode}.",
                    new Dictionary<string, object?>())
                {
                    Steps = stepResults,
                    Version = currentContext.Version,
                    Artifacts = artifactEntries,
                    PushDecisions = pushDecisionEntries,
                };
            }
        }

        return new CommandResult(
            commandName,
            true,
            0,
            $"Command '{commandName}' completed successfully.",
            new Dictionary<string, object?>())
        {
            Steps = stepResults,
            Version = currentContext.Version,
            Artifacts = artifactEntries,
            PushDecisions = pushDecisionEntries,
        };
    }

    private static async Task WriteSarifIfConfiguredAsync(
        IReadOnlyList<Analysis.AnalysisResult> results,
        string repositoryRoot,
        RepoConfig config,
        CancellationToken cancellationToken)
    {
        // Write SARIF output when an analysis configuration is present and specifies a configuration path
        if (config.Analysis?.Configuration is not null)
        {
            var sarifPath = System.IO.Path.IsPathRooted(config.Analysis.Configuration)
                ? config.Analysis.Configuration
                : System.IO.Path.Combine(repositoryRoot, config.Analysis.Configuration);

            // Only write SARIF if the path ends with .sarif or .json (to avoid overwriting arbitrary paths)
            if (sarifPath.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase) ||
                sarifPath.EndsWith(".sarif.json", StringComparison.OrdinalIgnoreCase))
            {
                await Analysis.DotnetAnalysisRunner.WriteSarifReportAsync(results, sarifPath, cancellationToken);
                Console.WriteLine($"  SARIF report written to: {sarifPath}");
            }
        }
    }

    private async Task<StepResult> BuildArtifactsAsync(
        string stepId,
        RepoConfig config,
        ExecutionContext ctx,
        Func<RepoArtifactConfig, bool> includePredicate,
        string successMessage,
        string emptyMessage,
        CancellationToken cancellationToken)
    {
        var artifacts = (config.Artifacts ?? [])
            .Where(includePredicate)
            .ToList();

        if (artifacts.Count == 0)
        {
            return new StepResult(stepId, true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = emptyMessage });
        }

        foreach (var artifactCfg in artifacts)
        {
            var provider = _artifactProviders.Resolve(artifactCfg.Type);
            if (provider is null)
            {
                Console.Error.WriteLine($"  Warning: No provider found for artifact type '{artifactCfg.Type}'.");
                continue;
            }

            var artifactConfig = ToArtifactConfig(artifactCfg);
            var result = await provider.BuildAsync(artifactConfig, ctx, cancellationToken);
            if (!result.Success)
            {
                return new StepResult(stepId, false, 5, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = $"Failed to build artifact '{artifactCfg.Name}'." });
            }
        }

        return new StepResult(stepId, true, 0, TimeSpan.Zero,
            new Dictionary<string, object?> { ["message"] = successMessage });
    }

    private async Task<StepResult> TagArtifactsAsync(
        string stepId,
        RepoConfig config,
        ExecutionContext ctx,
        Func<RepoArtifactConfig, bool> includePredicate,
        string successMessage,
        string emptyMessage,
        CancellationToken cancellationToken)
    {
        var artifacts = (config.Artifacts ?? [])
            .Where(includePredicate)
            .ToList();

        if (artifacts.Count == 0)
        {
            return new StepResult(stepId, true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = emptyMessage });
        }

        foreach (var artifactCfg in artifacts)
        {
            var provider = _artifactProviders.Resolve(artifactCfg.Type);
            if (provider is null)
            {
                continue;
            }

            await provider.TagAsync(ToArtifactConfig(artifactCfg), ctx, cancellationToken);
        }

        return new StepResult(stepId, true, 0, TimeSpan.Zero,
            new Dictionary<string, object?> { ["message"] = successMessage });
    }

    private async Task<StepResult> PushArtifactsAsync(
        string stepId,
        RepoConfig config,
        string repositoryRoot,
        ExecutionContext ctx,
        Func<RepoArtifactConfig, bool> includePredicate,
        string successMessage,
        string emptyMessage,
        CancellationToken cancellationToken)
    {
        var artifacts = (config.Artifacts ?? [])
            .Where(includePredicate)
            .ToList();

        if (artifacts.Count == 0)
        {
            return new StepResult(stepId, true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = emptyMessage });
        }

        var manifestEntries = new List<Core.Models.ArtifactManifestEntry>();
        var pushDecisions = new List<Core.Models.PushDecision>();
        var globalPolicy = ParsePushPolicyRules(config.PushRulesJson);

        foreach (var artifactCfg in artifacts)
        {
            var provider = _artifactProviders.Resolve(artifactCfg.Type);
            if (provider is null)
            {
                continue;
            }

            var effectivePolicy = BuildEffectivePushPolicy(globalPolicy, artifactCfg.Settings);
            if (!IsPushAllowed(effectivePolicy, ctx, out var gateReason))
            {
                manifestEntries.Add(new Core.Models.ArtifactManifestEntry(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    Built: true,
                    Pushed: false,
                    Tags: Array.Empty<string>()));
                pushDecisions.Add(new Core.Models.PushDecision(artifactCfg.Name, false, gateReason));
                continue;
            }

            var pushResult = await provider.PushAsync(ToArtifactConfig(artifactCfg), ctx, cancellationToken);
            var pushPerformed = pushResult.PublishedReferences.Count > 0;
            manifestEntries.Add(new Core.Models.ArtifactManifestEntry(
                artifactCfg.Type,
                artifactCfg.Name,
                Built: true,
                Pushed: pushPerformed,
                Tags: pushResult.PublishedReferences));
            pushDecisions.Add(new Core.Models.PushDecision(
                artifactCfg.Name,
                pushResult.Success,
                pushResult.Success
                    ? (pushPerformed ? "Push succeeded." : "Push skipped.")
                    : $"Failed to push artifact '{artifactCfg.Name}'."));

            if (!pushResult.Success)
            {
                await WriteArtifactManifestAsync(repositoryRoot, manifestEntries, cancellationToken);
                return new StepResult(stepId, false, 6, TimeSpan.Zero,
                    new Dictionary<string, object?>
                    {
                        ["error"] = $"Failed to push artifact '{artifactCfg.Name}'.",
                        ["__artifacts"] = manifestEntries,
                        ["__pushDecisions"] = pushDecisions,
                    });
            }
        }

        await WriteArtifactManifestAsync(repositoryRoot, manifestEntries, cancellationToken);
        return new StepResult(stepId, true, 0, TimeSpan.Zero,
            new Dictionary<string, object?>
            {
                ["message"] = successMessage,
                ["__artifacts"] = manifestEntries,
                ["__pushDecisions"] = pushDecisions,
            });
    }

    private static StepResult PlanArtifacts(
        string stepId,
        RepoConfig config,
        Func<RepoArtifactConfig, bool> includePredicate,
        string successMessage,
        string emptyMessage)
    {
        var artifacts = (config.Artifacts ?? [])
            .Where(includePredicate)
            .ToList();

        if (artifacts.Count == 0)
        {
            return new StepResult(stepId, true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = emptyMessage });
        }

        var plans = artifacts.Select(a => new
        {
            a.Type,
            a.Name,
            Image = TryGetArtifactSettingString(a.Settings, "image") ?? a.Name,
            Dockerfile = TryGetArtifactSettingString(a.Settings, "dockerfile") ?? "Dockerfile",
            Context = TryGetArtifactSettingString(a.Settings, "context") ?? ".",
            Runner = TryGetArtifactSettingString(a.Settings, "runner") ?? "build",
            Stages = TryGetArtifactSettingObject(a.Settings, "stages")?.Select(p => p.Name).ToArray() ?? Array.Empty<string>(),
        }).ToList();

        var json = JsonSerializer.Serialize(plans, IndentedJsonOptions);
        Console.WriteLine(json);

        return new StepResult(stepId, true, 0, TimeSpan.Zero,
            new Dictionary<string, object?>
            {
                ["message"] = successMessage,
                ["plan"] = json,
            });
    }

    private static ArtifactConfig ToArtifactConfig(RepoArtifactConfig artifactCfg) =>
        new(
            artifactCfg.Type,
            artifactCfg.Name,
            artifactCfg.Settings ?? new Dictionary<string, JsonElement>());

    private static string? TryGetArtifactSettingString(Dictionary<string, JsonElement>? settings, string key)
    {
        if (settings is null || !settings.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            _ => value.ToString(),
        };
    }

    private static IEnumerable<JsonProperty>? TryGetArtifactSettingObject(Dictionary<string, JsonElement>? settings, string key)
    {
        if (settings is null || !settings.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.EnumerateObject();
    }

    private static Dictionary<string, JsonElement> CloneSettings(Dictionary<string, JsonElement> settings)
    {
        var clone = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in settings)
        {
            clone[key] = value.Clone();
        }

        return clone;
    }

    private static IReadOnlyDictionary<string, string?> BuildOptionsWithDefaults(
        IReadOnlyDictionary<string, string?> provided,
        RepoCommandConfig commandConfig)
    {
        var result = new Dictionary<string, string?>(provided, StringComparer.OrdinalIgnoreCase);

        foreach (var (optName, optConfig) in commandConfig.Options ?? [])
        {
            if (!result.ContainsKey(optName) && optConfig.Default is not null)
            {
                result[optName] = optConfig.Default;
            }
        }

        return result;
    }

    private static RepoCommandConfig NormalizeCommandConfig(RepoCommandConfig commandConfig) =>
        new(
            commandConfig.Description,
            commandConfig.Options ?? [],
            commandConfig.Steps ?? [])
        {
            Args = commandConfig.Args ?? [],
            MaxParallel = commandConfig.MaxParallel,
        };

    private static async Task WriteArtifactManifestAsync(
        string repositoryRoot,
        IReadOnlyList<Core.Models.ArtifactManifestEntry> entries,
        CancellationToken cancellationToken)
    {
        var artifactsDir = Path.Combine(repositoryRoot, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        var manifestPath = Path.Combine(artifactsDir, "manifest.json");

        var manifest = new
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTimeOffset.UtcNow,
            Artifacts = entries,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest, IndentedJsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
        Console.WriteLine($"  Artifact manifest written to {manifestPath}");
    }

    private static PushPolicyRules ParsePushPolicyRules(string? pushRulesJson)
    {
        if (string.IsNullOrWhiteSpace(pushRulesJson))
        {
            return PushPolicyRules.Default;
        }

        try
        {
            using var doc = JsonDocument.Parse(pushRulesJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return PushPolicyRules.Default;
            }

            return new PushPolicyRules(
                Enabled: ReadBoolean(root, "enabled") ?? true,
                NoPushInPullRequest: ReadBoolean(root, "noPushInPullRequest") ?? false,
                RequireCleanWorkingTree: ReadBoolean(root, "requireCleanWorkingTree") ?? false,
                Branches: ReadStringList(root, "branches") ?? []);
        }
        catch (JsonException)
        {
            // Malformed rules JSON — fall back to defaults.
            return PushPolicyRules.Default;
        }
    }

    private static PushPolicyRules BuildEffectivePushPolicy(
        PushPolicyRules globalPolicy,
        Dictionary<string, JsonElement>? artifactSettings)
    {
        if (artifactSettings is null || artifactSettings.Count == 0)
        {
            return globalPolicy;
        }

        var enabled = TryGetBool(artifactSettings, "push.enabled")
            ?? TryGetBool(artifactSettings, "pushEnabled");
        var noPushInPullRequest = TryGetBool(artifactSettings, "push.noPushInPullRequest")
            ?? TryGetBool(artifactSettings, "push.noPushInPr")
            ?? TryGetBool(artifactSettings, "noPushInPullRequest");
        var requireCleanWorkingTree = TryGetBool(artifactSettings, "push.requireCleanWorkingTree")
            ?? TryGetBool(artifactSettings, "requireCleanWorkingTree");
        var branches = TryGetStringList(artifactSettings, "push.branches")
            ?? TryGetStringList(artifactSettings, "pushBranches");

        return globalPolicy with
        {
            Enabled = enabled ?? globalPolicy.Enabled,
            NoPushInPullRequest = noPushInPullRequest ?? globalPolicy.NoPushInPullRequest,
            RequireCleanWorkingTree = requireCleanWorkingTree ?? globalPolicy.RequireCleanWorkingTree,
            Branches = branches ?? globalPolicy.Branches,
        };
    }

    private static bool IsPushAllowed(
        PushPolicyRules policy,
        Core.Models.ExecutionContext ctx,
        out string reason)
    {
        if (!policy.Enabled)
        {
            reason = "Push skipped by policy: disabled.";
            return false;
        }

        if (policy.NoPushInPullRequest && ctx.IsPullRequest)
        {
            reason = "Push skipped by policy: pull request context.";
            return false;
        }

        if (policy.RequireCleanWorkingTree && !ctx.IsCleanWorkingTree)
        {
            reason = "Push skipped by policy: working tree is not clean.";
            return false;
        }

        if (policy.Branches.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(ctx.Branch))
            {
                reason = "Push skipped by policy: branch is unknown.";
                return false;
            }

            if (!policy.Branches.Any(pattern => BranchMatches(pattern, ctx.Branch!)))
            {
                reason = $"Push skipped by policy: branch '{ctx.Branch}' not allowed.";
                return false;
            }
        }

        reason = "Push allowed by policy.";
        return true;
    }

    private static bool BranchMatches(string pattern, string branch)
    {
        if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.IsMatch(branch, pattern["regex:".Length..], RegexOptions.CultureInvariant);
        }

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(branch, regex, RegexOptions.CultureInvariant);
    }

    private static bool? TryGetBool(Dictionary<string, JsonElement> settings, string path)
    {
        if (!TryGetValue(settings, path, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? ReadBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static List<string>? ReadStringList(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => SplitDelimitedList(value.GetString() ?? string.Empty),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToList(),
            _ => null,
        };
    }

    private static List<string>? TryGetStringList(Dictionary<string, JsonElement> settings, string path)
    {
        if (!TryGetValue(settings, path, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => SplitDelimitedList(value.GetString() ?? string.Empty),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToList(),
            _ => null,
        };
    }

    private static bool TryGetValue(Dictionary<string, JsonElement> settings, string path, out JsonElement value)
    {
        value = default;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !settings.TryGetValue(parts[0], out value))
        {
            return false;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(parts[i], out value))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> SplitDelimitedList(string value) =>
        value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private sealed record PushPolicyRules(
        bool Enabled = true,
        bool NoPushInPullRequest = false,
        bool RequireCleanWorkingTree = false,
        List<string> Branches = null!)
    {
        public static PushPolicyRules Default => new(Branches: []);
    }

    private static StepDefinition BuildStepDefinition(RepoStepConfig stepConfig) =>
        new(
            Id: stepConfig.Id,
            Run: stepConfig.Run,
            Uses: stepConfig.Uses,
            Command: stepConfig.Command,
            When: stepConfig.When)
        {
            Parallel = stepConfig.Parallel ?? false,
            ContinueOnError = stepConfig.ContinueOnError ?? false,
            OutputPattern = stepConfig.OutputPattern,
            OutputFile = stepConfig.OutputFile,
        };

    /// <summary>
    /// Groups consecutive steps marked <c>parallel: true</c> into batches.
    /// Sequential steps (parallel == false) form singleton groups.
    /// </summary>
    private static List<List<RepoStepConfig>> GroupSteps(IEnumerable<RepoStepConfig> steps)
    {
        var groups = new List<List<RepoStepConfig>>();
        List<RepoStepConfig>? currentGroup = null;

        foreach (var step in steps)
        {
            if (step.Parallel == true)
            {
                currentGroup ??= [];
                currentGroup.Add(step);
            }
            else
            {
                if (currentGroup is { Count: > 0 })
                {
                    groups.Add(currentGroup);
                    currentGroup = null;
                }
                groups.Add([step]);
            }
        }

        if (currentGroup is { Count: > 0 })
        {
            groups.Add(currentGroup);
        }

        return groups;
    }

    private static async Task<List<(RepoStepConfig Config, StepResult Result)>> ExecuteParallelGroupAsync(
        IReadOnlyList<RepoStepConfig> group,
        StepExecutor stepExecutor,
        ExecutionContext snapshot,
        int? maxParallel,
        CancellationToken cancellationToken)
    {
        var pending = group
            .Select(sc => (Config: sc, Definition: BuildStepDefinition(sc)))
            .ToList();

        var results = new List<(RepoStepConfig Config, StepResult Result)>();
        var completedInGroup = new Dictionary<string, StepResult>(StringComparer.OrdinalIgnoreCase);
        var knownGroupStepIds = pending
            .Select(p => p.Definition.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Validate missing dependency references before execution starts.
        foreach (var step in pending)
        {
            foreach (var dependencyId in GetDependencies(step.Config))
            {
                var foundInGroup = knownGroupStepIds.Contains(dependencyId);
                var foundInCompleted = snapshot.CompletedSteps.ContainsKey(dependencyId);
                if (foundInGroup || foundInCompleted)
                {
                    continue;
                }

                var stepId = step.Definition.Id ?? step.Config.Id ?? "parallel-step";
                var failed = new StepResult(
                    stepId,
                    false,
                    1,
                    TimeSpan.Zero,
                    new Dictionary<string, object?>
                    {
                        ["error"] = $"Step dependency '{dependencyId}' was not found for step '{stepId}'.",
                    });

                results.Add((step.Config, failed));
                return results;
            }
        }

        while (pending.Count > 0)
        {
            var ready = pending
                .Where(step => DependenciesSatisfied(step.Config, snapshot.CompletedSteps, completedInGroup))
                .ToList();

            if (ready.Count == 0)
            {
                var unresolved = pending
                    .Select(p => p.Definition.Id ?? p.Config.Id ?? "<unnamed>")
                    .ToArray();
                var first = pending[0];
                var stepId = first.Definition.Id ?? first.Config.Id ?? "parallel-step";
                var failed = new StepResult(
                    stepId,
                    false,
                    1,
                    TimeSpan.Zero,
                    new Dictionary<string, object?>
                    {
                        ["error"] =
                            $"Parallel dependency deadlock/cycle detected. Unresolved steps: {string.Join(", ", unresolved)}.",
                    });

                results.Add((first.Config, failed));
                return results;
            }

            var stageContext = BuildStageContext(snapshot, completedInGroup.Values);

            List<(RepoStepConfig Config, StepResult Result)> stageResults;
            if (maxParallel is > 0)
            {
                using var semaphore = new SemaphoreSlim(maxParallel.Value, maxParallel.Value);
                var tasks = ready.Select(async step =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await stepExecutor.ExecuteAsync(step.Definition, stageContext, cancellationToken);
                        return (step.Config, result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                stageResults = [.. await Task.WhenAll(tasks)];
            }
            else
            {
                var tasks = ready.Select(async step =>
                {
                    var result = await stepExecutor.ExecuteAsync(step.Definition, stageContext, cancellationToken);
                    return (step.Config, result);
                });
                stageResults = [.. await Task.WhenAll(tasks)];
            }

            foreach (var stageResult in stageResults)
            {
                results.Add(stageResult);
                completedInGroup[stageResult.Result.StepId] = stageResult.Result;
            }

            foreach (var readyStep in ready)
            {
                pending.Remove(readyStep);
            }
        }

        return results;
    }

    private static IEnumerable<string> GetDependencies(RepoStepConfig step) =>
        step.DependsOn is not { Length: > 0 }
            ? []
            : step.DependsOn
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim());

    private static bool DependenciesSatisfied(
        RepoStepConfig step,
        IReadOnlyDictionary<string, StepResult> previouslyCompleted,
        IReadOnlyDictionary<string, StepResult> completedInGroup)
    {
        foreach (var dependencyId in GetDependencies(step))
        {
            if (!previouslyCompleted.ContainsKey(dependencyId) && !completedInGroup.ContainsKey(dependencyId))
            {
                return false;
            }
        }

        return true;
    }

    private static ExecutionContext BuildStageContext(
        ExecutionContext snapshot,
        IEnumerable<StepResult> completedInGroup)
    {
        var context = snapshot;
        foreach (var stepResult in completedInGroup)
        {
            context = context.WithStep(stepResult);
            if (stepResult.Outputs.TryGetValue("__version", out var versionObj) && versionObj is VersionResult version)
            {
                context = context.WithVersion(version);
            }
        }

        return context;
    }
}
