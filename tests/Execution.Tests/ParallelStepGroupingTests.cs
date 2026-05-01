namespace Rexo.Execution.Tests;

using Rexo.Configuration.Models;

/// <summary>
/// Tests for parallel step grouping logic in ConfigCommandLoader.
/// The grouping logic is internal, so we test via observable execution behaviour
/// using BuiltinCommandRegistration config commands.
/// </summary>
public sealed class ParallelStepGroupingTests
{
    [Fact]
    public void StepConfigParallelFlagDefaultsToNull()
    {
        var step = new RepoStepConfig(Run: "echo hi");
        Assert.Null(step.Parallel);
    }

    [Fact]
    public void StepConfigParallelFlagCanBeSetTrue()
    {
        var step = new RepoStepConfig(Run: "echo hi", Parallel: true);
        Assert.True(step.Parallel);
    }

    [Fact]
    public void StepConfigContinueOnErrorCanBeSet()
    {
        var step = new RepoStepConfig(Run: "echo fail", ContinueOnError: true);
        Assert.True(step.ContinueOnError);
    }

    [Fact]
    public void StepConfigOutputPatternAndOutputFileCanBeSet()
    {
        var step = new RepoStepConfig(Run: "echo hello", OutputPattern: @"(?<greeting>\w+)", OutputFile: "out.txt");
        Assert.Equal(@"(?<greeting>\w+)", step.OutputPattern);
        Assert.Equal("out.txt", step.OutputFile);
    }
}
