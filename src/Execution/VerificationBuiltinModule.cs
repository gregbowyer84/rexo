namespace Rexo.Execution;

using Rexo.Core.Models;

internal sealed class VerificationBuiltinModule : IConfigBuiltinModule
{
    public void Register(BuiltinRegistry registry, ConfigBuiltinModuleContext context)
    {
        registry.Register("builtin:test", async (step, ctx, ct) =>
        {
            var testsConfig = context.Config.Tests;
            var verificationConfig = new Verification.VerificationConfig(
                Enabled: testsConfig?.Enabled ?? true,
                Projects: testsConfig?.Projects,
                Configuration: testsConfig?.Configuration ?? "Release",
                OutputRoot: ConfigCommandLoader.ResolveOutputRoot(context.Config),
                ResultsOutput: testsConfig?.ResultsOutput,
                CoverageOutput: testsConfig?.CoverageOutput,
                LineCoverageThreshold: testsConfig?.CoverageThreshold,
                BranchCoverageThreshold: null);

            var result = await Verification.DotnetTestRunner.RunAsync(verificationConfig, context.RepositoryRoot, ct);

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

        registry.Register("builtin:analyze", async (step, ctx, ct) =>
        {
            var analysisResults = new List<Analysis.AnalysisResult>();

            var formatResult = await Analysis.DotnetAnalysisRunner.RunFormatCheckAsync(context.RepositoryRoot, ct);
            analysisResults.Add(formatResult);
            if (!formatResult.Success && (context.Config.Analysis?.FailOnIssues ?? true))
            {
                return new StepResult(step.Id ?? "analyze", false, 1, TimeSpan.Zero,
                    new Dictionary<string, object?> { ["error"] = string.Join("; ", formatResult.Issues) });
            }

            if (context.Config.Analysis?.Tools is { Length: > 0 })
            {
                foreach (var toolCmd in context.Config.Analysis.Tools)
                {
                    if (string.IsNullOrWhiteSpace(toolCmd))
                    {
                        continue;
                    }

                    var toolResult = await Analysis.DotnetAnalysisRunner.RunCustomToolAsync(toolCmd, context.RepositoryRoot, ct);
                    analysisResults.Add(toolResult);

                    if (!toolResult.Success && context.Config.Analysis.FailOnIssues)
                    {
                        await ConfigCommandLoader.WriteSarifIfConfiguredAsync(analysisResults, context.RepositoryRoot, context.Config, ct);

                        return new StepResult(step.Id ?? "analyze", false, 1, TimeSpan.Zero,
                            new Dictionary<string, object?> { ["error"] = string.Join("; ", toolResult.Issues) });
                    }
                }
            }

            await ConfigCommandLoader.WriteSarifIfConfiguredAsync(analysisResults, context.RepositoryRoot, context.Config, ct);

            return new StepResult(step.Id ?? "analyze", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "Analysis passed." });
        });

        registry.Register("builtin:verify", async (step, ctx, ct) =>
        {
            Console.WriteLine("  Running verification (test + analyze)...");
            if (registry.TryResolve("builtin:test", out var testBuiltin) && testBuiltin is not null)
            {
                var testResult = await testBuiltin(step, ctx, ct);
                if (!testResult.Success)
                {
                    return testResult with { StepId = step.Id ?? "verify" };
                }
            }

            if (registry.TryResolve("builtin:analyze", out var analyzeBuiltin) && analyzeBuiltin is not null)
            {
                var analyzeResult = await analyzeBuiltin(step, ctx, ct);
                if (!analyzeResult.Success)
                {
                    return analyzeResult with { StepId = step.Id ?? "verify" };
                }
            }

            return new StepResult(step.Id ?? "verify", true, 0, TimeSpan.Zero,
                new Dictionary<string, object?> { ["message"] = "Verification passed." });
        });
    }
}
