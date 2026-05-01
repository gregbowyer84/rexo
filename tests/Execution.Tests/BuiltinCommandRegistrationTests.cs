namespace Rexo.Execution.Tests;

using Rexo.Configuration.Models;
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

    [Fact]
    public async Task ConfigResolvedReturnsSuccessWhenConfigProvided()
    {
        var config = new RepoConfig(
            Name: "test",
            Commands: [],
            Aliases: [])
        { SchemaVersion = "1.0" };
        var registry = BuiltinCommandRegistration.CreateDefault(config, configPath: null);
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("config resolved", EmptyInvocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("schemaVersion", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigResolvedReturnsFailureWhenNoConfigProvided()
    {
        var registry = BuiltinCommandRegistration.CreateDefault(config: null, configPath: null);
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("config resolved", EmptyInvocation(), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExplainVersionReturnsProviderInfo()
    {
        var config = new RepoConfig(
            Name: "test",
            Commands: [],
            Aliases: [])
        {
            SchemaVersion = "1.0",
            Versioning = new RepoVersioningConfig(Provider: "fixed", Fallback: "1.0.0"),
        };
        var registry = BuiltinCommandRegistration.CreateDefault(config, configPath: null);
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("explain version", EmptyInvocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("fixed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplainVersionReturnsMessageWhenNoVersioningConfig()
    {
        var registry = BuiltinCommandRegistration.CreateDefault(config: null, configPath: null);
        var executor = new DefaultCommandExecutor(registry);

        var result = await executor.ExecuteAsync("explain version", EmptyInvocation(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("No versioning", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoctorReportsEmbeddedSchemaFallbackWhenNoLocalSchema()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-doctor-noschema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, ".rexo"));
        await File.WriteAllTextAsync(Path.Combine(dir, ".rexo", "rexo.json"), "{}");

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);
            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>(),
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("doctor", invocation, CancellationToken.None);

            Assert.Contains("schema", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("embedded fallback", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DoctorReportsLocalSchemaWhenPresent()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-doctor-localschema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, ".rexo"));
        await File.WriteAllTextAsync(Path.Combine(dir, ".rexo", "rexo.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), "{}");

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);
            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>(),
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("doctor", invocation, CancellationToken.None);

            Assert.Contains("schema", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("local", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
