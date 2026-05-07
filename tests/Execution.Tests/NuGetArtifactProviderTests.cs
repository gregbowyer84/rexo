namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts.NuGet;
using Rexo.Core.Models;

public sealed class NuGetArtifactProviderTests
{
    [Fact]
    public async Task PushAsyncUsesDefaultSourceFromEnvironment()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, "https://pkgs.dev.azure.com/acme/_packaging/shared/nuget/v3/index.json");
        Environment.SetEnvironmentVariable(keyEnvName, "test-key");

        try
        {
            var invocations = new List<string>();
            var provider = new NuGetArtifactProvider(
                runDotnetAsync: (arguments, workingDirectory, cancellationToken) =>
                {
                    invocations.Add(arguments);
                    return Task.FromResult((0, string.Empty));
                });

            var artifact = new ArtifactConfig(
                "nuget",
                "Rexo.Core",
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    """
                    {
                      "output": "artifacts/packages"
                    }
                    """)!);

            var result = await provider.PushAsync(artifact, ExecutionContext.Empty(Path.GetTempPath()), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(invocations);
            Assert.Contains("--source https://pkgs.dev.azure.com/acme/_packaging/shared/nuget/v3/index.json", invocations[0], StringComparison.Ordinal);
            Assert.Contains("--api-key test-key", invocations[0], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }

    [Fact]
    public async Task PushAsyncUsesTargetSourceWhenEnvironmentSourceIsNotSet()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, null);
        Environment.SetEnvironmentVariable(keyEnvName, "test-key");

        try
        {
            var invocations = new List<string>();
            var provider = new NuGetArtifactProvider(
                runDotnetAsync: (arguments, workingDirectory, cancellationToken) =>
                {
                    invocations.Add(arguments);
                    return Task.FromResult((0, string.Empty));
                });

            var artifact = new ArtifactConfig(
                "nuget",
                "Rexo.Core",
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    """
                    {
                      "output": "artifacts/packages",
                                            "target": {
                                                "source": "https://nuget.pkg.github.com/acme/index.json"
                                            }
                    }
                    """)!);

            var result = await provider.PushAsync(artifact, ExecutionContext.Empty(Path.GetTempPath()), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(invocations);
            Assert.Contains("--source https://nuget.pkg.github.com/acme/index.json", invocations[0], StringComparison.Ordinal);
            Assert.DoesNotContain("--source https://api.nuget.org/v3/index.json", invocations[0], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }
}
