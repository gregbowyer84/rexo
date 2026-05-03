namespace Rexo.Configuration.Tests;

using Rexo.Cli;
using Rexo.Configuration.Models;

/// <summary>
/// Tests for ConfigBuilder, including --set override handling.
/// </summary>
public sealed class ConfigBuilderTests
{
    [Fact]
    public void ApplySetOverridesWithWarningsReturnsWarningsForMalformedOverrides()
    {
        var config = new RepoConfig(
            Name: "test",
            Commands: null,
            Aliases: null)
        { SchemaVersion = "1.0" };

        var overrides = new[] { "versioning.provider=env", "malformed-no-equals", "another.valid=true" };

        var (resultConfig, warnings) = ConfigBuilder.ApplySetOverridesWithWarnings(config, overrides);

        Assert.NotNull(resultConfig);
        Assert.Single(warnings);
        Assert.Contains("malformed-no-equals", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("key.path=value", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplySetOverridesWithWarningsReturnsWarningsForEmptyKeyPath()
    {
        var config = new RepoConfig(
            Name: "test",
            Commands: null,
            Aliases: null)
        { SchemaVersion = "1.0" };

        var overrides = new[] { "=value-with-empty-key" };

        var (resultConfig, warnings) = ConfigBuilder.ApplySetOverridesWithWarnings(config, overrides);

        Assert.NotNull(resultConfig);
        Assert.Single(warnings);
        Assert.Contains("empty", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplySetOverridesWithWarningsAppliesValidOverridesAndCollectsWarnings()
    {
        var config = new RepoConfig(
            Name: "original",
            Commands: null,
            Aliases: null)
        {
            SchemaVersion = "1.0",
            Versioning = new RepoVersioningConfig("fixed", "1.0.0")
        };

        var overrides = new[] { "versioning.fallback=2.0.0", "invalid-override", "name=updated" };

        var (resultConfig, warnings) = ConfigBuilder.ApplySetOverridesWithWarnings(config, overrides);

        Assert.NotNull(resultConfig);
        Assert.Equal("updated", resultConfig.Name);
        Assert.Equal("2.0.0", resultConfig.Versioning?.Fallback);
        Assert.Single(warnings);
    }

    [Fact]
    public void ApplySetOverridesWithWarningsReturnsEmptyWarningsForValidOverrides()
    {
        var config = new RepoConfig(
            Name: "test",
            Commands: null,
            Aliases: null)
        { SchemaVersion = "1.0" };

        var overrides = new[] { "name=updated" };

        var (resultConfig, warnings) = ConfigBuilder.ApplySetOverridesWithWarnings(config, overrides);

        Assert.NotNull(resultConfig);
        Assert.Empty(warnings);
        Assert.Equal("updated", resultConfig.Name);
    }
}
