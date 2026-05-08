namespace Rexo.Execution.Tests;

using System.Text.RegularExpressions;
using System.Text.Json;
using Rexo.Artifacts.NuGet;
using Rexo.Core.Models;

[Collection("EnvVar Mutation Sequential")]
public sealed class NuGetArtifactProviderTests
{
    private static readonly string[] DiversePackageNames =
    [
        "Common.RuntimeLicensing",
        "Common.RuntimeLicensing.AspNetCore",
        "Common.RuntimeLicensing.Remote",
        "Common.RuntimeLicensing.Authority",
        "Acme.Core",
        "Acme.Core.Extensions",
        "Acme.Core.Extensions.Http",
        "Acme-Tools.Cli",
        "Acme.Tools.Cli.V2",
        "Acme2.Runtime",
        "X",
        "X.Y",
    ];

    private static readonly string[] DiverseNonPackageFiles =
    [
        "artifacts/packages/Common.RuntimeLicensing.AspNetCore.symbols.nupkg",
        "artifacts/packages/Common.RuntimeLicensing.readme.txt",
        "artifacts/packages/Acme.Core.Extensions.Http.nuspec",
    ];

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
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.[0-9]*.nupkg\"", invocations[0], StringComparison.Ordinal);
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
            Assert.Contains("nuget push \"artifacts/packages/Rexo.Core.[0-9]*.nupkg\"", invocations[0], StringComparison.Ordinal);
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

    [Fact]
    public async Task PushAsyncFallbackPatternAvoidsPrefixPackageIdCollisions()
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
                "Common.RuntimeLicensing",
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    """
                    {
                      "output": "artifacts/packages"
                    }
                    """)!);

            var result = await provider.PushAsync(artifact, ExecutionContext.Empty(Path.GetTempPath()), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(invocations);
            Assert.Contains("nuget push \"artifacts/packages/Common.RuntimeLicensing.[0-9]*.nupkg\"", invocations[0], StringComparison.Ordinal);
            Assert.DoesNotContain("Common.RuntimeLicensing.*.nupkg", invocations[0], StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }

    [Fact]
    public async Task PushAsyncFallbackPatternIsStableAcrossDiversePackageNames()
    {
        const string sourceEnvName = "NUGET_TARGET_SOURCE";
        const string keyEnvName = "NUGET_API_KEY";
        var originalSource = Environment.GetEnvironmentVariable(sourceEnvName);
        var originalApiKey = Environment.GetEnvironmentVariable(keyEnvName);

        Environment.SetEnvironmentVariable(sourceEnvName, "https://api.nuget.org/v3/index.json");
        Environment.SetEnvironmentVariable(keyEnvName, "test-key");

        try
        {
            var candidateFiles = DiversePackageNames
                .Select(name => $"artifacts/packages/{name}.1.2.3-alpha.4.nupkg")
                .Concat(DiverseNonPackageFiles)
                .ToArray();

            foreach (var packageName in DiversePackageNames)
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
                    packageName,
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        """
                        {
                          "output": "artifacts/packages"
                        }
                        """)!);

                var result = await provider.PushAsync(artifact, ExecutionContext.Empty(Path.GetTempPath()), CancellationToken.None);

                Assert.True(result.Success);
                Assert.Single(invocations);

                var expectedPattern = $"artifacts/packages/{packageName}.[0-9]*.nupkg";
                Assert.Contains($"nuget push \"{expectedPattern}\"", invocations[0], StringComparison.Ordinal);

                var matchingFiles = candidateFiles
                    .Where(file => WildcardMatch(file, expectedPattern))
                    .ToArray();

                Assert.Single(matchingFiles);
                Assert.Equal($"artifacts/packages/{packageName}.1.2.3-alpha.4.nupkg", matchingFiles[0]);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceEnvName, originalSource);
            Environment.SetEnvironmentVariable(keyEnvName, originalApiKey);
        }
    }

    private static bool WildcardMatch(string input, string wildcardPattern)
    {
        var regexPattern = "^" + GlobToRegex(wildcardPattern) + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.CultureInvariant);
    }

    private static string GlobToRegex(string pattern)
    {
        var builder = new System.Text.StringBuilder();

        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '*':
                    builder.Append(".*");
                    break;
                case '?':
                    builder.Append('.');
                    break;
                case '[':
                    {
                        var close = pattern.IndexOf(']', i + 1);
                        if (close > i)
                        {
                            builder.Append(pattern.AsSpan(i, close - i + 1));
                            i = close;
                        }
                        else
                        {
                            builder.Append("\\[");
                        }

                        break;
                    }
                default:
                    builder.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        return builder.ToString();
    }
}
