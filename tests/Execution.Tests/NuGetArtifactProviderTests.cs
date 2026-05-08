namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Artifacts.NuGet;
using Rexo.Core.Models;

[Collection("EnvVar Mutation Sequential")]
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
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.*.nupkg\"", invocations[0], StringComparison.Ordinal);
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
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.*.nupkg\"", invocations[0], StringComparison.Ordinal);
            Assert.Contains("--source https://nuget.pkg.github.com/acme/index.json", invocations[0], StringComparison.Ordinal);
            Assert.DoesNotContain("--source https://api.nuget.org/v3/index.json", invocations[0], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }

    [Fact]
    public async Task PushAsyncUsesVersionQualifiedPackageWhenVersionResolved()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, "https://api.nuget.org/v3/index.json");
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

            var context = ExecutionContext.Empty(Path.GetTempPath()) with
            {
                Version = new VersionResult(
                    SemVer: "1.2.3-alpha.4",
                    Major: 1,
                    Minor: 2,
                    Patch: 3,
                    PreRelease: "alpha.4",
                    CommitSha: "abc123",
                    ShortSha: "abc123",
                    IsPreRelease: true,
                    IsStable: false)
                {
                    NuGetVersion = "1.2.3-alpha.4",
                },
            };

            var result = await provider.PushAsync(artifact, context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(invocations);
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.1.2.3-alpha.4.nupkg\"", invocations[0], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }

    [Fact]
    public async Task PushAsyncPushesSymbolsWhenEnabled()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, "https://api.nuget.org/v3/index.json");
        Environment.SetEnvironmentVariable(keyEnvName, "test-key");

        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"rexo-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repositoryRoot, "artifacts", "packages"));

        try
        {
            File.WriteAllText(
                Path.Combine(repositoryRoot, "artifacts", "packages", "Rexo.Core.1.2.3-alpha.4.snupkg"),
                "symbols");

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
                      "symbols": {
                        "enabled": true
                      }
                    }
                    """)!);

            var context = ExecutionContext.Empty(repositoryRoot) with
            {
                Version = new VersionResult(
                    SemVer: "1.2.3-alpha.4",
                    Major: 1,
                    Minor: 2,
                    Patch: 3,
                    PreRelease: "alpha.4",
                    CommitSha: "abc123",
                    ShortSha: "abc123",
                    IsPreRelease: true,
                    IsStable: false)
                {
                    NuGetVersion = "1.2.3-alpha.4",
                },
            };

            var result = await provider.PushAsync(artifact, context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, invocations.Count);
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.1.2.3-alpha.4.nupkg\"", invocations[0], StringComparison.Ordinal);
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.1.2.3-alpha.4.snupkg\"", invocations[1], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PushAsyncUsesCustomSymbolPatternWhenConfigured()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, "https://api.nuget.org/v3/index.json");
        Environment.SetEnvironmentVariable(keyEnvName, "test-key");

        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"rexo-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repositoryRoot, "artifacts", "packages"));

        try
        {
            File.WriteAllText(
                Path.Combine(repositoryRoot, "artifacts", "packages", "Common.RuntimeLicensing.symbols.nupkg"),
                "symbols");

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
                      "symbols": {
                        "enabled": true,
                        "pattern": "artifacts/packages/*.symbols.nupkg"
                      }
                    }
                    """)!);

            var context = ExecutionContext.Empty(repositoryRoot) with
            {
                Version = new VersionResult(
                    SemVer: "1.2.3-alpha.4",
                    Major: 1,
                    Minor: 2,
                    Patch: 3,
                    PreRelease: "alpha.4",
                    CommitSha: "abc123",
                    ShortSha: "abc123",
                    IsPreRelease: true,
                    IsStable: false)
                {
                    NuGetVersion = "1.2.3-alpha.4",
                },
            };

            var result = await provider.PushAsync(artifact, context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, invocations.Count);
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.1.2.3-alpha.4.nupkg\"", invocations[0], StringComparison.Ordinal);
            Assert.Contains("nuget push \"artifacts/packages/*.symbols.nupkg\"", invocations[1], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }
}
