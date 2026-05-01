namespace Rexo.Execution.Tests;

using Rexo.Core.Models;

public sealed class BuiltinCommandRegistrationTests
{
    private static CommandInvocation EmptyInvocation() =>
        new(
            new Dictionary<string, string>(),
            new Dictionary<string, string?>(),
            Json: false,
            JsonFile: null,
            WorkingDirectory: "C:\\repo");

    [Fact]
    public async Task VersionCommandReturnsSuccess()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("version", EmptyInvocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("version", result.Command);
    }

    [Fact]
    public async Task ListCommandReturnsSuccess()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("list", EmptyInvocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ExplainCommandWithNoArgReturnsMeaningfulResult()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var invocation = new CommandInvocation(
            new Dictionary<string, string> { ["command"] = "version" },
            new Dictionary<string, string?>(),
            Json: false,
            JsonFile: null,
            WorkingDirectory: "C:\\repo");

        var result = await executor.ExecuteAsync("explain", invocation, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UnknownCommandReturnsExitCode8()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("definitely-not-a-command", EmptyInvocation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
    }

    [Fact]
    public void RegistryExposedViaExecutor()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        Assert.NotNull(executor.Registry);
        Assert.True(executor.Registry.TryResolve("version", out _));
    }
}
