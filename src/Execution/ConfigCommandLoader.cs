namespace Rexo.Execution;

using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Abstractions;
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
        _builtinRegistry.Register("builtin:build-artifacts", async (step, ctx, ct) =>
        {
            if (config.Artifacts is null || config.Artifacts.Count == 0)
            {
                return new StepResult(step.Id ?? "build-artifacts", true, 0, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["message"] = "No artifacts configured." });
            }

            foreach (var artifactCfg in config.Artifacts)
            {
                var provider = _artifactProviders.Resolve(artifactCfg.Type);
                if (provider is null)
                {
                    Console.Error.WriteLine($"  Warning: No provider found for artifact type '{artifactCfg.Type}'.");
                    continue;
                }

                var artifactConfig = new ArtifactConfig(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    artifactCfg.Settings ?? new Dictionary<string, string>());

                var result = await provider.BuildAsync(artifactConfig, ctx, ct);
                if (!result.Success)
                {
                    return new StepResult(step.Id ?? "build-artifacts", false, 5, TimeSpan.Zero,
                        new Dictionary<string, object?> { ["error"] = $"Failed to build artifact '{artifactCfg.Name}'." });
                }
            }

            return new StepResult(step.Id ?? "build-artifacts", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "All artifacts built." });
        });

        // builtin:tag-artifacts
        _builtinRegistry.Register("builtin:tag-artifacts", async (step, ctx, ct) =>
        {
            if (config.Artifacts is null || config.Artifacts.Count == 0)
            {
                return new StepResult(step.Id ?? "tag-artifacts", true, 0, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["message"] = "No artifacts configured." });
            }

            foreach (var artifactCfg in config.Artifacts)
            {
                var provider = _artifactProviders.Resolve(artifactCfg.Type);
                if (provider is null) continue;

                var artifactConfig = new ArtifactConfig(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    artifactCfg.Settings ?? new Dictionary<string, string>());

                await provider.TagAsync(artifactConfig, ctx, ct);
            }

            return new StepResult(step.Id ?? "tag-artifacts", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "All artifacts tagged." });
        });

        // builtin:push-artifacts
        _builtinRegistry.Register("builtin:push-artifacts", async (step, ctx, ct) =>
        {
            if (config.Artifacts is null || config.Artifacts.Count == 0)
            {
                return new StepResult(step.Id ?? "push-artifacts", true, 0, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["message"] = "No artifacts configured." });
            }

            // Push policy enforcement
            var policyViolation = CheckPushPolicy(config.PushRulesJson, ctx);
            if (policyViolation is not null)
            {
                var deniedDecisions = (config.Artifacts ?? [])
                    .Select(a => new Core.Models.PushDecision(a.Name, false, policyViolation))
                    .ToList();
                return new StepResult(step.Id ?? "push-artifacts", false, 7, TimeSpan.Zero,
                    new Dictionary<string, object?>
                    {
                        ["error"] = policyViolation,
                        ["__pushDecisions"] = deniedDecisions,
                    });
            }

            var manifestEntries = new List<Core.Models.ArtifactManifestEntry>();
            var pushDecisions = new List<Core.Models.PushDecision>();

            foreach (var artifactCfg in config.Artifacts)
            {
                var provider = _artifactProviders.Resolve(artifactCfg.Type);
                if (provider is null) continue;

                var artifactConfig = new ArtifactConfig(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    artifactCfg.Settings ?? new Dictionary<string, string>());

                var pushResult = await provider.PushAsync(artifactConfig, ctx, ct);
                var entry = new Core.Models.ArtifactManifestEntry(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    Built: true,
                    Pushed: pushResult.Success,
                    Tags: pushResult.PublishedReferences);
                manifestEntries.Add(entry);
                pushDecisions.Add(new Core.Models.PushDecision(
                    artifactCfg.Name,
                    pushResult.Success,
                    pushResult.Success ? "Push succeeded." : $"Failed to push artifact '{artifactCfg.Name}'."));

                if (!pushResult.Success)
                {
                    await WriteArtifactManifestAsync(repositoryRoot, manifestEntries, ct);
                    return new StepResult(step.Id ?? "push-artifacts", false, 6, TimeSpan.Zero,
                        new Dictionary<string, object?>
                        {
                            ["error"] = $"Failed to push artifact '{artifactCfg.Name}'.",
                            ["__artifacts"] = manifestEntries,
                            ["__pushDecisions"] = pushDecisions,
                        });
                }
            }

            await WriteArtifactManifestAsync(repositoryRoot, manifestEntries, ct);

            return new StepResult(step.Id ?? "push-artifacts", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["message"] = "All artifacts pushed.",
                    ["__artifacts"] = manifestEntries,
                    ["__pushDecisions"] = pushDecisions,
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
            Options = BuildOptionsWithDefaults(invocation.Options, commandConfig),
        };

        var stepExecutor = new StepExecutor(commandExecutor, _templateRenderer, _builtinRegistry);
        var stepResults = new List<StepResult>();
        var currentContext = context;
        var artifactEntries = new List<Core.Models.ArtifactManifestEntry>();
        var pushDecisionEntries = new List<Core.Models.PushDecision>();

        // Group consecutive parallel steps; sequential steps are singleton groups
        var stepGroups = GroupSteps(commandConfig.Steps);

        foreach (var group in stepGroups)
        {
            List<StepResult> groupResults;

            if (group.Count == 1)
            {
                var stepConfig = group[0];
                var stepDef = BuildStepDefinition(stepConfig);
                var stepResult = await stepExecutor.ExecuteAsync(stepDef, currentContext, cancellationToken);
                groupResults = [stepResult];
            }
            else
            {
                // Run parallel group concurrently — all steps share a snapshot of currentContext
                // Honour maxParallel if set on the command (0 / null = no cap)
                var snapshot = currentContext;
                var maxParallel = commandConfig.MaxParallel;

                if (maxParallel is > 0)
                {
                    using var semaphore = new SemaphoreSlim(maxParallel.Value, maxParallel.Value);
                    var tasks = group.Select(async sc =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            return await stepExecutor.ExecuteAsync(BuildStepDefinition(sc), snapshot, cancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    groupResults = [.. await Task.WhenAll(tasks)];
                }
                else
                {
                    var tasks = group.Select(sc =>
                        stepExecutor.ExecuteAsync(BuildStepDefinition(sc), snapshot, cancellationToken));
                    groupResults = [.. await Task.WhenAll(tasks)];
                }
            }

            foreach (var stepResult in groupResults)
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
            var failed = groupResults
                .Zip(group, (r, c) => (Result: r, Config: c))
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

    private static IReadOnlyDictionary<string, string?> BuildOptionsWithDefaults(
        IReadOnlyDictionary<string, string?> provided,
        RepoCommandConfig commandConfig)
    {
        var result = new Dictionary<string, string?>(provided, StringComparer.OrdinalIgnoreCase);

        foreach (var (optName, optConfig) in commandConfig.Options)
        {
            if (!result.ContainsKey(optName) && optConfig.Default is not null)
            {
                result[optName] = optConfig.Default;
            }
        }

        return result;
    }

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

    /// <summary>
    /// Evaluates push policy rules against the current execution context.
    /// Returns a violation message if a rule is violated, or null if push is allowed.
    /// </summary>
    private static string? CheckPushPolicy(string? pushRulesJson, Core.Models.ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(pushRulesJson)) return null;

        try
        {
            var rules = System.Text.Json.JsonSerializer.Deserialize<PushPolicyRules>(pushRulesJson);
            if (rules is null) return null;

            if (rules.NoPushInPullRequest && ctx.IsPullRequest)
                return "Push policy violation: push is not allowed in pull requests.";

            if (rules.RequireCleanWorkingTree && !ctx.IsCleanWorkingTree)
                return "Push policy violation: working tree has uncommitted changes.";
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed rules JSON — skip enforcement
        }

        return null;
    }

    private sealed record PushPolicyRules(
        bool NoPushInPullRequest = false,
        bool RequireCleanWorkingTree = false);

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
}
