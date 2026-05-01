namespace Rexo.Execution;

using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;
using Rexo.Versioning;

public sealed class ConfigCommandLoader
{
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
                    ["isPrerelease"] = versionResult.IsPreRelease.ToString().ToLowerInvariant(),
                    ["isStable"] = versionResult.IsStable.ToString().ToLowerInvariant(),
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

            foreach (var artifactCfg in config.Artifacts)
            {
                var provider = _artifactProviders.Resolve(artifactCfg.Type);
                if (provider is null) continue;

                var artifactConfig = new ArtifactConfig(
                    artifactCfg.Type,
                    artifactCfg.Name,
                    artifactCfg.Settings ?? new Dictionary<string, string>());

                var result = await provider.PushAsync(artifactConfig, ctx, ct);
                if (!result.Success)
                {
                    return new StepResult(step.Id ?? "push-artifacts", false, 6, TimeSpan.Zero,
                        new Dictionary<string, object?> { ["error"] = $"Failed to push artifact '{artifactCfg.Name}'." });
                }
            }

            return new StepResult(step.Id ?? "push-artifacts", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "All artifacts pushed." });
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
                LineCoverageThreshold: null,
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
            var formatResult = await Analysis.DotnetAnalysisRunner.RunFormatCheckAsync(repositoryRoot, ct);
            if (!formatResult.Success)
            {
                return new StepResult(step.Id ?? "analyze", false, 1, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = string.Join("; ", formatResult.Issues) });
            }

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
            Args = invocation.Args,
            Options = BuildOptionsWithDefaults(invocation.Options, commandConfig),
        };

        var stepExecutor = new StepExecutor(commandExecutor, _templateRenderer, _builtinRegistry);
        var stepResults = new List<StepResult>();
        var currentContext = context;

        foreach (var stepConfig in commandConfig.Steps)
        {
            var stepDef = new StepDefinition(
                Id: stepConfig.Id,
                Run: stepConfig.Run,
                Uses: stepConfig.Uses,
                Command: stepConfig.Command,
                When: stepConfig.When);

            var stepResult = await stepExecutor.ExecuteAsync(stepDef, currentContext, cancellationToken);
            stepResults.Add(stepResult);

            // Apply step to context (for future steps to reference outputs)
            currentContext = currentContext.WithStep(stepResult);

            // If this step resolved a version, propagate it to context
            if (stepResult.Outputs.TryGetValue("__version", out var versionObj) &&
                versionObj is VersionResult versionResult)
            {
                currentContext = currentContext.WithVersion(versionResult);
            }

            if (!stepResult.Success && stepConfig.ContinueOnError != true)
            {
                return new CommandResult(
                    commandName,
                    false,
                    stepResult.ExitCode,
                    $"Step '{stepResult.StepId}' failed with exit code {stepResult.ExitCode}.",
                    new Dictionary<string, object?>())
                { Steps = stepResults };
            }
        }

        return new CommandResult(
            commandName,
            true,
            0,
            $"Command '{commandName}' completed successfully.",
            new Dictionary<string, object?>())
        { Steps = stepResults };
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
}
