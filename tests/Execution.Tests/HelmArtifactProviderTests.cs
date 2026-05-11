namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts.Helm;
using Rexo.Core.Models;

public sealed class HelmArtifactProviderTests
{
    [Fact]
    public async Task BuildAsyncPackagesChartByDefault()
    {
        var invocations = new List<IReadOnlyList<string>>();
        var provider = new HelmArtifactProvider(
            runHelmAsync: (artifact, args, workingDirectory, dockerImage, cancellationToken) =>
            {
                invocations.Add(args.ToArray());
                return Task.FromResult((0, string.Empty));
            });

        var artifact = new ArtifactConfig(
            "helm",
            "orders-chart",
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """
                {
                  "chart-directory": "deploy/charts/orders",
                  "output-directory": "artifacts/charts"
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
            invocations[0]);
    }

    [Fact]
    public async Task BuildAsyncRetriesWithDependencyUpdateWhenPackageReportsMissingDependencies()
    {
        var invocations = new List<IReadOnlyList<string>>();
        var provider = new HelmArtifactProvider(
            runHelmAsync: (artifact, args, workingDirectory, dockerImage, cancellationToken) =>
            {
                invocations.Add(args.ToArray());
                if (invocations.Count == 1)
                {
                    return Task.FromResult((5, "Error: found in Chart.yaml, but missing in charts/ directory: ingress-nginx"));
                }

                return Task.FromResult((0, string.Empty));
            });

        var artifact = new ArtifactConfig(
            "helm",
            "orders-chart",
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """
                {
                  "chart-directory": "deploy/charts/orders",
                  "output-directory": "artifacts/charts"
                }
                """)!);

        var context = ExecutionContext.Empty(Path.GetTempPath()) with
        {
            Version = new VersionResult("1.2.3", 1, 2, 3, null, "abcdef123456", "abcdef", false, true),
        };

        var result = await provider.BuildAsync(artifact, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, invocations.Count);
        Assert.Equal("package", invocations[0][0]);
        Assert.Equal(["dependency", "update", "deploy/charts/orders"], invocations[1]);
        Assert.Equal("package", invocations[2][0]);
    }
}
