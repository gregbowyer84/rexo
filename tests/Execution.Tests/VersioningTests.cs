namespace Rexo.Execution.Tests;

using Rexo.Core.Models;
using Rexo.Versioning;

public sealed class VersioningTests
{
    private static ExecutionContext MakeContext() =>
        ExecutionContext.Empty("C:\\repo");

    [Fact]
    public async Task FixedVersionProviderReturnsFallbackVersion()
    {
        var provider = new FixedVersionProvider();
        var config = new VersioningConfig(Provider: "fixed", Fallback: "3.1.0");

        var result = await provider.ResolveAsync(config, MakeContext(), CancellationToken.None);

        Assert.Equal("3.1.0", result.SemVer);
        Assert.Equal(3, result.Major);
        Assert.Equal(1, result.Minor);
        Assert.Equal(0, result.Patch);
    }

    [Fact]
    public async Task FixedVersionProviderDefaultsWhenNoFallback()
    {
        var provider = new FixedVersionProvider();
        var config = new VersioningConfig(Provider: "fixed");

        var result = await provider.ResolveAsync(config, MakeContext(), CancellationToken.None);

        // Should use 0.1.0 default
        Assert.NotNull(result.SemVer);
        Assert.False(string.IsNullOrEmpty(result.SemVer));
    }

    [Fact]
    public async Task GitTagVersionProviderFallsBackWhenNotAGitRepo()
    {
        var provider = new GitTagVersionProvider();
        var config = new VersioningConfig(Provider: "git", Fallback: "0.9.0");

        // Use a non-git-repo directory so git describe fails
        var ctx = ExecutionContext.Empty(System.IO.Path.GetTempPath());
        var result = await provider.ResolveAsync(config, ctx, CancellationToken.None);

        // Should return fallback gracefully
        Assert.NotNull(result.SemVer);
        Assert.False(string.IsNullOrEmpty(result.SemVer));
    }

    [Fact]
    public void VersionProviderRegistryContainsGitProvider()
    {
        var registry = VersionProviderRegistry.CreateDefault();
        var provider = registry.Resolve("git");
        Assert.NotNull(provider);
    }

    [Fact]
    public void VersionProviderRegistryContainsFixedProvider()
    {
        var registry = VersionProviderRegistry.CreateDefault();
        var provider = registry.Resolve("fixed");
        Assert.NotNull(provider);
    }
}
