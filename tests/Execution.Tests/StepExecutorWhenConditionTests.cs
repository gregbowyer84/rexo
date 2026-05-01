namespace Rexo.Execution.Tests;

using Rexo.Core.Models;
using Rexo.Templating;

/// <summary>
/// Tests for StepExecutor: when-condition evaluation, continueOnError behaviour,
/// and unknown-builtin handling.
/// </summary>
public sealed class StepExecutorWhenConditionTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static StepExecutor CreateExecutor(BuiltinRegistry? registry = null)
    {
        var builtins = registry ?? new BuiltinRegistry();
        var cmdRegistry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(cmdRegistry);
        var renderer = new TemplateRenderer();
        return new StepExecutor(executor, renderer, builtins);
    }

    private static ExecutionContext EmptyContext() =>
        ExecutionContext.Empty(Path.GetTempPath());

    private static BuiltinRegistry TrackingRegistry(out List<string> called)
    {
        var log = new List<string>();
        called = log;
        var registry = new BuiltinRegistry();
        registry.Register("builtin:noop", (step, _, _) =>
        {
            log.Add(step.Id ?? "noop");
            return Task.FromResult(new StepResult(
                step.Id ?? "noop", true, 0,
                TimeSpan.Zero,
                new Dictionary<string, object?>()));
        });
        return registry;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // when = "true" / truthy values → step executes
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("YES")]
    [InlineData("anything-else")]
    public async Task WhenTruthyConditionStepIsExecuted(string condition)
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var step = new StepDefinition(
            Id: "my-step",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: condition);

        await executor.ExecuteAsync(step, EmptyContext(), CancellationToken.None);

        Assert.Contains("my-step", log);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // when = "false" / falsy values → step is skipped
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("NO")]
    public async Task WhenFalsyConditionStepIsSkipped(string condition)
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var step = new StepDefinition(
            Id: "my-step",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: condition);

        var result = await executor.ExecuteAsync(step, EmptyContext(), CancellationToken.None);

        Assert.Empty(log);                           // handler was never called
        Assert.True(result.Success);                 // skipped is not a failure
        Assert.Equal("true", result.Outputs["skipped"]?.ToString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // when renders a template expression: {{options.deploy}} == "true"
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenConditionRendersTemplateVariables()
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Options = new Dictionary<string, string?> { ["deploy"] = "true" }
        };

        var step = new StepDefinition(
            Id: "s1",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: "{{options.deploy}}");

        await executor.ExecuteAsync(step, context, CancellationToken.None);

        Assert.Contains("s1", log);
    }

    [Fact]
    public async Task WhenConditionEqualityExpressionTrueRunsStep()
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Options = new Dictionary<string, string?> { ["env"] = "prod" }
        };

        var step = new StepDefinition(
            Id: "s2",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: "{{options.env == \"prod\"}}");

        await executor.ExecuteAsync(step, context, CancellationToken.None);

        Assert.Contains("s2", log);
    }

    [Fact]
    public async Task WhenConditionEqualityExpressionFalseSkipsStep()
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Options = new Dictionary<string, string?> { ["env"] = "dev" }
        };

        var step = new StepDefinition(
            Id: "s3",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: "{{options.env == \"prod\"}}");

        var result = await executor.ExecuteAsync(step, context, CancellationToken.None);

        Assert.Empty(log);
        Assert.True(result.Success);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // No when condition → step always runs
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWhenConditionStepAlwaysRuns()
    {
        var registry = TrackingRegistry(out var log);
        var executor = CreateExecutor(registry);

        var step = new StepDefinition(
            Id: "s4",
            Run: null,
            Uses: "builtin:noop",
            Command: null,
            When: null);

        await executor.ExecuteAsync(step, EmptyContext(), CancellationToken.None);

        Assert.Contains("s4", log);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Unknown builtin → failure result, not an exception
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownBuiltinProducesFailureResult()
    {
        var executor = CreateExecutor();

        var step = new StepDefinition(
            Id: "bad",
            Run: null,
            Uses: "builtin:does-not-exist",
            Command: null,
            When: null);

        var result = await executor.ExecuteAsync(step, EmptyContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("does-not-exist", result.Outputs["error"]?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step with no run/uses/command → failure result
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StepWithNoActionProducesFailureResult()
    {
        var executor = CreateExecutor();

        var step = new StepDefinition(
            Id: "empty",
            Run: null,
            Uses: null,
            Command: null,
            When: null);

        var result = await executor.ExecuteAsync(step, EmptyContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("no run, uses, or command", result.Outputs["error"]?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
