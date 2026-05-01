namespace Rexo.Core.Tests;

using Rexo.Core.Models;

public sealed class CommandResultTests
{
    [Fact]
    public void OkCreatesSuccessfulResult()
    {
        var result = CommandResult.Ok("version", "0.1.0-local");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("version", result.Command);
    }

    [Fact]
    public void FailCreatesFailedResult()
    {
        var result = CommandResult.Fail("run", 8, "Command was not found.");

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        Assert.Equal("run", result.Command);
    }
}
