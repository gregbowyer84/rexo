namespace Rexo.Integration.Tests;

using Rexo.Cli;

public sealed class CliSmokeTests
{
    [Fact]
    public async Task VersionCommandReturnsSuccess()
    {
        var exitCode = await Program.ExecuteAsync(["version"], CancellationToken.None);
        Assert.Equal(0, exitCode);
    }
}
