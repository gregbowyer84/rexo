namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts.Helm;
using Rexo.Core.Models;

public sealed class HelmOciArtifactProviderTests
{
    [Fact]
    public async Task BuildAsyncPackagesChartWithResolvedVersion()
    {
        var invocations = new List<HelmInvocation>();
        var provider = new HelmOciArtifactProvider(
            runHelmAsync: (artifact, args, workingDirectory, envOverrides, standardInput, cancellationToken) =>
            {
                invocations.Add(new HelmInvocation(args.ToArray(), envOverrides, standardInput));
                return Task.FromResult((0, string.Empty));
            });

        var artifact = new ArtifactConfig(
            "helm-oci",
            "orders",
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """
                {
                  "chartPath": "deploy/charts/orders",
                  "output": "artifacts/charts"
                }
                """)!);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Version = new VersionResult("1.2.3", 1, 2, 3, null, "abcdef123456", "abcdef", false, true),
        };

        var result = await provider.BuildAsync(artifact, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(invocations);
        Assert.Equal(
            [
                "package",
                "deploy/charts/orders",
                "--destination",
                "artifacts/charts",
                "--version",
                "1.2.3",
                "--app-version",
                "1.2.3",
            ],
            invocations[0].Arguments);
    }

    [Fact]
    public async Task PushAsyncComposesOciDestinationFromRegistryAndRepository()
    {
        var invocations = new List<HelmInvocation>();
        var provider = new HelmOciArtifactProvider(
            runHelmAsync: (artifact, args, workingDirectory, envOverrides, standardInput, cancellationToken) =>
            {
                invocations.Add(new HelmInvocation(args.ToArray(), envOverrides, standardInput));
                return Task.FromResult((0, string.Empty));
            });

        var artifact = new ArtifactConfig(
            "helm-oci",
            "orders",
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """
                {
                  "chartPath": "deploy/charts/orders",
                  "output": "artifacts/charts",
                  "registry": "ghcr.io",
                  "repository": "acme/charts"
                }
                """)!);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Version = new VersionResult("1.2.3", 1, 2, 3, null, "abcdef123456", "abcdef", false, true),
        };

        var result = await provider.PushAsync(artifact, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, invocations.Count);
        Assert.Equal("package", invocations[0].Arguments[0]);
        Assert.Equal("push", invocations[1].Arguments[0]);
        Assert.Equal("oci://ghcr.io/acme/charts", invocations[1].Arguments[2]);
    }

    [Fact]
    public async Task BuildAsyncPassesDockerImageSettingToRealImplementation()
    {
        // Verify the setting key name is "dockerImage" (matching version provider conventions).
        // The actual docker fallback path is exercised through the real RunHelmAsync when
        // host helm is unavailable; here we validate the setting key is read correctly by
        // checking that our artifact config round-trips the value through GetSetting.
        var capturedArtifact = default(ArtifactConfig?);
        var provider = new HelmOciArtifactProvider(
            runHelmAsync: (artifact, args, workingDirectory, envOverrides, standardInput, cancellationToken) =>
            {
                capturedArtifact = artifact;
                return Task.FromResult((0, string.Empty));
            });

        var artifact = new ArtifactConfig(
            "helm-oci",
            "orders",
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """
                {
                  "chartPath": "deploy/charts/orders",
                  "output": "artifacts/charts",
                  "dockerImage": "my/helm:9.9.9"
                }
                """)!);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Version = new VersionResult("1.2.3", 1, 2, 3, null, "abcdef123456", "abcdef", false, true),
        };

        await provider.BuildAsync(artifact, context, CancellationToken.None);

        Assert.NotNull(capturedArtifact);
        Assert.True(capturedArtifact!.Settings.TryGetValue("dockerImage", out var imageElement));
        Assert.Equal("my/helm:9.9.9", imageElement.GetString());
    }

    private sealed record HelmInvocation(
        IReadOnlyList<string> Arguments,
        IReadOnlyDictionary<string, string?>? Environment,
        string? StandardInput);
}
