namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Configuration.Models;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;
using Rexo.Execution;
using Rexo.Templating;
using Rexo.Versioning;

public sealed class PushGateTests
{
    [Fact]
    public async Task GlobalPushGateCanBeOverriddenPerArtifact()
    {
        var provider = new RecordingArtifactProvider("docker");
        var (executor, _) = CreateExecutor(
            provider,
            new RepoConfig(
                Name: "test",
                Commands: new Dictionary<string, RepoCommandConfig>
                {
                    ["push"] = new RepoCommandConfig(
                        Description: "push",
                        Options: new Dictionary<string, RepoOptionConfig>(),
                        Steps: [new RepoStepConfig(Id: "push", Uses: "builtin:push-artifacts")]),
                },
                Aliases: new Dictionary<string, string>())
            {
                Artifacts =
                [
                    new RepoArtifactConfig("docker", "a"),
                    new RepoArtifactConfig(
                        "docker",
                        "b",
                        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                            """
                            {
                              "push": {
                                "enabled": true
                              }
                            }
                            """)!),
                ],
                PushRulesJson = """
                    {
                      "enabled": false
                    }
                    """,
            });

        var result = await ExecuteAsync(executor, "push");

        Assert.True(result.Success);
        Assert.Single(provider.PushedArtifacts);
        Assert.Equal("b", provider.PushedArtifacts[0]);

        Assert.Equal(2, result.PushDecisions.Count);
        var denied = result.PushDecisions.Single(d => d.ArtifactName == "a");
        Assert.False(denied.Allowed);
        Assert.Contains("disabled", denied.Reason, StringComparison.OrdinalIgnoreCase);

        var allowed = result.PushDecisions.Single(d => d.ArtifactName == "b");
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public async Task BranchGateCanBeOverriddenPerArtifact()
    {
        var provider = new RecordingArtifactProvider("docker");
        var (executor, _) = CreateExecutor(
            provider,
            new RepoConfig(
                Name: "test",
                Commands: new Dictionary<string, RepoCommandConfig>
                {
                    ["push"] = new RepoCommandConfig(
                        Description: "push",
                        Options: new Dictionary<string, RepoOptionConfig>(),
                        Steps: [new RepoStepConfig(Id: "push", Uses: "builtin:push-artifacts")]),
                },
                Aliases: new Dictionary<string, string>())
            {
                Artifacts =
                [
                    new RepoArtifactConfig("docker", "a"),
                    new RepoArtifactConfig(
                        "docker",
                        "b",
                        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                            """
                            {
                              "push": {
                                "branches": []
                              }
                            }
                            """)!),
                ],
                PushRulesJson = """
                    {
                      "branches": ["main"]
                    }
                    """,
            });

        var result = await ExecuteAsync(executor, "push");

        Assert.True(result.Success);
        Assert.Single(provider.PushedArtifacts);
        Assert.Equal("b", provider.PushedArtifacts[0]);

        var denied = result.PushDecisions.Single(d => d.ArtifactName == "a");
        Assert.False(denied.Allowed);
        Assert.Contains("branch", denied.Reason, StringComparison.OrdinalIgnoreCase);

        var allowed = result.PushDecisions.Single(d => d.ArtifactName == "b");
        Assert.True(allowed.Allowed);
    }

    private static (DefaultCommandExecutor Executor, BuiltinRegistry Builtins) CreateExecutor(
        IArtifactProvider provider,
        RepoConfig config)
    {
        var builtins = new BuiltinRegistry();
        var providerRegistry = new ArtifactProviderRegistry();
        providerRegistry.Register(provider.Type, provider);

        var loader = new ConfigCommandLoader(
            builtins,
            new TemplateRenderer(),
            VersionProviderRegistry.CreateDefault(),
            providerRegistry);

        var registry = new CommandRegistry();
        var executor = new DefaultCommandExecutor(registry);
        loader.LoadInto(registry, config, Path.GetTempPath(), executor);
        return (executor, builtins);
    }

    private static Task<CommandResult> ExecuteAsync(DefaultCommandExecutor executor, string commandName) =>
        executor.ExecuteAsync(
            commandName,
            new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>(),
                Json: false,
                JsonFile: null,
                WorkingDirectory: Path.GetTempPath()),
            CancellationToken.None);

    private sealed class RecordingArtifactProvider(string type) : IArtifactProvider
    {
        public string Type { get; } = type;
        public List<string> PushedArtifacts { get; } = [];

        public Task<ArtifactBuildResult> BuildAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtifactBuildResult(artifact.Name, true, null));

        public Task<ArtifactTagResult> TagAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtifactTagResult(artifact.Name, true, Array.Empty<string>()));

        public Task<ArtifactPushResult> PushAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken)
        {
            PushedArtifacts.Add(artifact.Name);
            return Task.FromResult(new ArtifactPushResult(artifact.Name, true, [$"{artifact.Name}:latest"]));
        }
    }
}
