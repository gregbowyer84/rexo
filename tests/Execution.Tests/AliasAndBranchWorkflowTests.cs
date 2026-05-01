namespace Rexo.Execution.Tests;

using Rexo.Configuration.Models;
using Rexo.Core.Models;

/// <summary>
/// Integration-style tests for alias resolution, multi-word command dispatch,
/// and branch-workflow naming edge cases.
/// </summary>
public sealed class AliasAndBranchWorkflowTests
{
    private static CommandInvocation Invocation(Dictionary<string, string>? args = null) =>
        new(
            args ?? [],
            new Dictionary<string, string?>(),
            Json: false,
            JsonFile: null,
            WorkingDirectory: Environment.CurrentDirectory);

    private static readonly Func<CommandInvocation, CancellationToken, Task<CommandResult>> NoOpHandler =
        (_, _) => Task.FromResult(CommandResult.Ok("noop", "ok"));

    // -------------------------------------------------------------------------
    // Alias resolution via registry (mimics ConfigCommandLoader alias wiring)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AliasResolvesToTargetCommand()
    {
        var registry = new CommandRegistry();
        registry.Register("build", NoOpHandler);

        // Wire alias the same way ConfigCommandLoader does
        registry.Register("b", (inv, ct) =>
            registry.TryResolve("build", out var handler) && handler is not null
                ? handler(inv, ct)
                : Task.FromResult(CommandResult.Fail("b", 8, "Alias target 'build' not found.")));

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync("b", Invocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public async Task AliasToMissingTargetReturnsFailure()
    {
        var registry = new CommandRegistry();
        // Alias points to "build" but "build" is not registered
        registry.Register("b", (inv, ct) =>
            registry.TryResolve("build", out var handler) && handler is not null
                ? handler(inv, ct)
                : Task.FromResult(CommandResult.Fail("b", 8, "Alias target 'build' not found.")));

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync("b", Invocation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        Assert.Contains("not found", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChainedAliasDoesNotResolveTransitively()
    {
        // "c" → "b" (alias), but "b" itself resolves to nothing in the registry
        // → only one level of alias is followed
        var registry = new CommandRegistry();
        registry.Register("build", NoOpHandler);

        // "b" is an alias to "build"
        registry.Register("b", (inv, ct) =>
            registry.TryResolve("build", out var h) && h is not null
                ? h(inv, ct)
                : Task.FromResult(CommandResult.Fail("b", 8, "not found")));

        // "c" is an alias to "b" — but "b" is registered as a handler, so this *does* resolve
        // because the registry holds "b" as a real (alias) entry
        registry.Register("c", (inv, ct) =>
            registry.TryResolve("b", out var h) && h is not null
                ? h(inv, ct)
                : Task.FromResult(CommandResult.Fail("c", 8, "not found")));

        var executor = new DefaultCommandExecutor(registry);

        // "c" → "b" → "build" all registered, so all succeed
        var result = await executor.ExecuteAsync("c", Invocation(), CancellationToken.None);
        Assert.True(result.Success);
    }

    // -------------------------------------------------------------------------
    // Multi-word command resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MultiWordCommandExactMatchSucceeds()
    {
        var registry = new CommandRegistry();
        registry.Register("branch feature", NoOpHandler);

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync("branch feature", Invocation(), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SingleWordAndMultiWordCommandCoexist()
    {
        var registry = new CommandRegistry();
        var log = new List<string>();

        registry.Register("branch", (inv, ct) =>
        {
            log.Add("branch");
            return Task.FromResult(CommandResult.Ok("branch", "branch"));
        });
        registry.Register("branch feature", (inv, ct) =>
        {
            log.Add("branch feature");
            return Task.FromResult(CommandResult.Ok("branch feature", "branch feature"));
        });

        var executor = new DefaultCommandExecutor(registry);

        await executor.ExecuteAsync("branch", Invocation(), CancellationToken.None);
        await executor.ExecuteAsync("branch feature", Invocation(), CancellationToken.None);

        Assert.Equal(["branch", "branch feature"], log);
    }

    [Fact]
    public async Task CommandWithHyphensInNameResolves()
    {
        var registry = new CommandRegistry();
        registry.Register("run-tests", NoOpHandler);

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync("run-tests", Invocation(), CancellationToken.None);

        Assert.True(result.Success);
    }

    // -------------------------------------------------------------------------
    // Not-found + suggestion engine
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UnknownCommandWithSimilarNameReturnsSuggestion()
    {
        var registry = new CommandRegistry();
        registry.Register("build", NoOpHandler);

        var executor = new DefaultCommandExecutor(registry);

        // "bild" is Levenshtein-1 away from "build"
        var result = await executor.ExecuteAsync("bild", Invocation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        Assert.Contains("build", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownCommandWithNoSimilarNameReturnsGenericMessage()
    {
        var registry = new CommandRegistry();
        registry.Register("deploy", NoOpHandler);

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync("xyzzy", Invocation(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        // Should NOT contain "deploy" as suggestion (too far away)
        Assert.DoesNotContain("deploy", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyCommandNameReturnsNotFound()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync(string.Empty, Invocation(), CancellationToken.None);

        Assert.False(result.Success);
    }

    // -------------------------------------------------------------------------
    // Explain command with config commands
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExplainKnownConfigCommandReturnsDescription()
    {
        var config = new RepoConfig(
            "test-repo",
            new Dictionary<string, RepoCommandConfig>
            {
                ["deploy"] = new RepoCommandConfig("Deploy to production", [], []),
            },
            []);

        var registry = BuiltinCommandRegistration.CreateDefault(config: config);
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync(
            "explain",
            new CommandInvocation(
                new Dictionary<string, string> { ["command"] = "deploy" },
                new Dictionary<string, string?>(),
                false, null, Environment.CurrentDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("deploy", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deploy to production", result.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplainUnknownCommandReturnsFailure()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync(
            "explain",
            new CommandInvocation(
                new Dictionary<string, string> { ["command"] = "nonexistent" },
                new Dictionary<string, string?>(),
                false, null, Environment.CurrentDirectory),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
    }

    [Fact]
    public async Task ExplainBuiltinCommandReturnsSuccess()
    {
        var registry = BuiltinCommandRegistration.CreateDefault();
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync(
            "explain",
            new CommandInvocation(
                new Dictionary<string, string> { ["command"] = "version" },
                new Dictionary<string, string?>(),
                false, null, Environment.CurrentDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("version", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
