namespace Rexo.Execution.Tests;

using Rexo.Core.Models;
using Rexo.Execution;

public sealed class DefaultCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsNotFoundForUnknownCommand()
    {
        var registry = new CommandRegistry();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync(
            "missing",
            new CommandInvocation(new Dictionary<string, string>(), new Dictionary<string, string?>(), false, null, Environment.CurrentDirectory),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
    }
}
