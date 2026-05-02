namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Configuration.Models;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;
using Rexo.Execution;
using Rexo.Templating;
using Rexo.Versioning;

public sealed class ArtifactWorkflowBuiltinTests
{
    [Fact]
    public async Task PlanAndDockerPlanDoNotInvokeProvidersAndReturnExpectedScope()
    {
        var dockerProvider = new RecordingArtifactProvider("docker");
        var nugetProvider = new RecordingArtifactProvider("nuget");
        var executor = CreateExecutor(dockerProvider, nugetProvider);

        var plan = await ExecuteAsync(executor, "plan");
        Assert.True(plan.Success);
        Assert.Empty(dockerProvider.BuildCalls);
        Assert.Empty(dockerProvider.TagCalls);
        Assert.Empty(dockerProvider.PushCalls);
        Assert.Empty(nugetProvider.BuildCalls);
        Assert.Empty(nugetProvider.TagCalls);
        Assert.Empty(nugetProvider.PushCalls);

        var allPlanItems = ParsePlan(plan, "plan");
        Assert.Equal(2, allPlanItems.Count);
        Assert.Contains(allPlanItems, item => item.Type == "docker" && item.Name == "docker-app");
        Assert.Contains(allPlanItems, item => item.Type == "nuget" && item.Name == "nuget-lib");

        var dockerPlan = await ExecuteAsync(executor, "docker-plan");
        Assert.True(dockerPlan.Success);

        var dockerPlanItems = ParsePlan(dockerPlan, "docker-plan");
        Assert.Single(dockerPlanItems);
        Assert.Equal("docker", dockerPlanItems[0].Type);
        Assert.Equal("docker-app", dockerPlanItems[0].Name);
    }

    [Fact]
    public async Task ShipAndAllApplyToAllArtifacts()
    {
        var dockerProvider = new RecordingArtifactProvider("docker");
        var nugetProvider = new RecordingArtifactProvider("nuget");
        var executor = CreateExecutor(dockerProvider, nugetProvider);

        var ship = await ExecuteAsync(executor, "ship");
        Assert.True(ship.Success);
        Assert.Empty(dockerProvider.BuildCalls);
        Assert.Empty(nugetProvider.BuildCalls);
        Assert.Equal(["docker-app"], dockerProvider.TagCalls);
        Assert.Equal(["docker-app"], dockerProvider.PushCalls);
        Assert.Equal(["nuget-lib"], nugetProvider.TagCalls);
        Assert.Equal(["nuget-lib"], nugetProvider.PushCalls);

        dockerProvider.Reset();
        nugetProvider.Reset();

        var all = await ExecuteAsync(executor, "all");
        Assert.True(all.Success);
        Assert.Equal(["docker-app"], dockerProvider.BuildCalls);
        Assert.Equal(["docker-app"], dockerProvider.TagCalls);
        Assert.Equal(["docker-app"], dockerProvider.PushCalls);
        Assert.Equal(["nuget-lib"], nugetProvider.BuildCalls);
        Assert.Equal(["nuget-lib"], nugetProvider.TagCalls);
        Assert.Equal(["nuget-lib"], nugetProvider.PushCalls);
    }

    [Fact]
    public async Task DockerScopedBuiltinsOnlyTouchDockerArtifacts()
    {
        var dockerProvider = new RecordingArtifactProvider("docker");
        var nugetProvider = new RecordingArtifactProvider("nuget");
        var executor = CreateExecutor(dockerProvider, nugetProvider);

        var ship = await ExecuteAsync(executor, "docker-ship");
        Assert.True(ship.Success);
        Assert.Equal(["docker-app"], dockerProvider.TagCalls);
        Assert.Equal(["docker-app"], dockerProvider.PushCalls);
        Assert.Empty(nugetProvider.TagCalls);
        Assert.Empty(nugetProvider.PushCalls);

        dockerProvider.Reset();
        nugetProvider.Reset();

        var all = await ExecuteAsync(executor, "docker-all");
        Assert.True(all.Success);
        Assert.Equal(["docker-app"], dockerProvider.BuildCalls);
        Assert.Equal(["docker-app"], dockerProvider.TagCalls);
        Assert.Equal(["docker-app"], dockerProvider.PushCalls);
        Assert.Empty(nugetProvider.BuildCalls);
        Assert.Empty(nugetProvider.TagCalls);
        Assert.Empty(nugetProvider.PushCalls);
    }

    [Fact]
    public async Task ArtifactNameFallsBackToRootConfigNameWhenOmitted()
    {
        var dockerProvider = new RecordingArtifactProvider("docker");
        var builtins = new BuiltinRegistry();
        var providerRegistry = new ArtifactProviderRegistry();
        providerRegistry.Register(dockerProvider.Type, dockerProvider);

        var loader = new ConfigCommandLoader(
            builtins,
            new TemplateRenderer(),
            VersionProviderRegistry.CreateDefault(),
            providerRegistry);

        var config = new RepoConfig(
            Name: "repo-root-name",
            Commands: new Dictionary<string, RepoCommandConfig>
            {
                ["build"] = new RepoCommandConfig(
                    Description: "build",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "build", Uses: "builtin:build-artifacts")]),
            },
            Aliases: new Dictionary<string, string>())
        {
            Artifacts =
            [
                new RepoArtifactConfig(
                    "docker",
                    null,
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        """
                        {
                          "image": "ghcr.io/acme/docker-app"
                        }
                        """)!),
            ],
        };

        var registry = new CommandRegistry();
        var executor = new DefaultCommandExecutor(registry);
        loader.LoadInto(registry, config, Path.GetTempPath(), executor);

        var result = await ExecuteAsync(executor, "build");

        Assert.True(result.Success);
        Assert.Equal(["repo-root-name"], dockerProvider.BuildCalls);
    }

    private static DefaultCommandExecutor CreateExecutor(params IArtifactProvider[] providers)
    {
        var builtins = new BuiltinRegistry();
        var providerRegistry = new ArtifactProviderRegistry();
        foreach (var provider in providers)
        {
            providerRegistry.Register(provider.Type, provider);
        }

        var loader = new ConfigCommandLoader(
            builtins,
            new TemplateRenderer(),
            VersionProviderRegistry.CreateDefault(),
            providerRegistry);

        var config = new RepoConfig(
            Name: "test",
            Commands: new Dictionary<string, RepoCommandConfig>
            {
                ["plan"] = new RepoCommandConfig(
                    Description: "plan",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "plan", Uses: "builtin:plan")]),
                ["ship"] = new RepoCommandConfig(
                    Description: "ship",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "ship", Uses: "builtin:ship")]),
                ["all"] = new RepoCommandConfig(
                    Description: "all",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "all", Uses: "builtin:all")]),
                ["docker-plan"] = new RepoCommandConfig(
                    Description: "docker-plan",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "docker-plan", Uses: "builtin:docker-plan")]),
                ["docker-ship"] = new RepoCommandConfig(
                    Description: "docker-ship",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "docker-ship", Uses: "builtin:docker-ship")]),
                ["docker-all"] = new RepoCommandConfig(
                    Description: "docker-all",
                    Options: new Dictionary<string, RepoOptionConfig>(),
                    Steps: [new RepoStepConfig(Id: "docker-all", Uses: "builtin:docker-all")]),
            },
            Aliases: new Dictionary<string, string>())
        {
            Artifacts =
            [
                new RepoArtifactConfig(
                    "docker",
                    "docker-app",
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
                    {
                      "image": "ghcr.io/acme/docker-app"
                    }
                    """)!),
                new RepoArtifactConfig(
                    "nuget",
                    "nuget-lib",
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
                    {
                      "project": "src/Core/Core.csproj"
                    }
                    """)!),
            ],
        };

        var registry = new CommandRegistry();
        var executor = new DefaultCommandExecutor(registry);
        loader.LoadInto(registry, config, Path.GetTempPath(), executor);
        return executor;
    }

    private static Task<CommandResult> ExecuteAsync(DefaultCommandExecutor executor, string commandName) =>
        executor.ExecuteAsync(
            commandName,
            new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?> { ["confirm"] = "true" },
                Json: false,
                JsonFile: null,
                WorkingDirectory: Path.GetTempPath()),
            CancellationToken.None);

    private static List<PlanItem> ParsePlan(CommandResult result, string stepId)
    {
        var step = result.Steps.Single(s => s.StepId == stepId);
        var json = Assert.IsType<string>(step.Outputs["plan"]);
        var parsed = JsonSerializer.Deserialize<List<PlanItem>>(json);
        return Assert.IsType<List<PlanItem>>(parsed);
    }

    private sealed class RecordingArtifactProvider(string type) : IArtifactProvider
    {
        public string Type { get; } = type;
        public List<string> BuildCalls { get; } = [];
        public List<string> TagCalls { get; } = [];
        public List<string> PushCalls { get; } = [];

        public Task<ArtifactBuildResult> BuildAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken)
        {
            BuildCalls.Add(artifact.Name);
            return Task.FromResult(new ArtifactBuildResult(artifact.Name, true, null));
        }

        public Task<ArtifactTagResult> TagAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken)
        {
            TagCalls.Add(artifact.Name);
            return Task.FromResult(new ArtifactTagResult(artifact.Name, true, []));
        }

        public Task<ArtifactPushResult> PushAsync(ArtifactConfig artifact, ExecutionContext context, CancellationToken cancellationToken)
        {
            PushCalls.Add(artifact.Name);
            return Task.FromResult(new ArtifactPushResult(artifact.Name, true, [$"{artifact.Name}:latest"]));
        }

        public void Reset()
        {
            BuildCalls.Clear();
            TagCalls.Clear();
            PushCalls.Clear();
        }
    }

    private sealed record PlanItem(string Type, string Name);
}
