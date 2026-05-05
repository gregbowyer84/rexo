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

    [Fact]
    public async Task LoadPoliciesFromSourcesAsyncLoadsLocalFilePolicyAndMerges()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-policy-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var policyFile = Path.Combine(dir, "team.policy.json");

        await File.WriteAllTextAsync(policyFile,
            """
            {
              "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json",
              "schemaVersion": "1.0",
              "name": "team-policy",
              "commands": {
                "team-check": {
                  "description": "Run team check",
                  "options": {},
                  "steps": [{ "run": "echo team" }]
                }
              },
              "aliases": {}
            }
            """);

        try
        {
            var result = await PolicySourceLoader.LoadPoliciesFromSourcesAsync(
                [policyFile],
                dir,
                debug: false,
                CancellationToken.None);

            Assert.NotNull(result.Commands);
            Assert.True(result.Commands!.ContainsKey("team-check"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task LoadPoliciesFromSourcesAsyncMergesMultipleSourcesInOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-policy-multi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var base1 = Path.Combine(dir, "base.policy.json");
        var override1 = Path.Combine(dir, "override.policy.json");

        await File.WriteAllTextAsync(base1,
            """
            {
              "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json",
              "schemaVersion": "1.0",
              "name": "base-policy",
              "commands": {
                "shared-cmd": {
                  "description": "Base version",
                  "options": {},
                  "steps": [{ "run": "echo base" }]
                },
                "base-only": {
                  "description": "Only in base",
                  "options": {},
                  "steps": [{ "run": "echo base-only" }]
                }
              },
              "aliases": {}
            }
            """);

        await File.WriteAllTextAsync(override1,
            """
            {
              "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json",
              "schemaVersion": "1.0",
              "name": "override-policy",
              "commands": {
                "shared-cmd": {
                  "description": "Override version",
                  "options": {},
                  "steps": [{ "run": "echo override" }]
                }
              },
              "aliases": {}
            }
            """);

        try
        {
            var result = await PolicySourceLoader.LoadPoliciesFromSourcesAsync(
                [base1, override1],
                dir,
                debug: false,
                CancellationToken.None);

            Assert.NotNull(result.Commands);
            // override1 wins for shared-cmd
            Assert.Equal("Override version", result.Commands!["shared-cmd"].Description);
            // base-only survives from base1
            Assert.True(result.Commands.ContainsKey("base-only"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task LoadPoliciesFromSourcesAsyncEmptyListReturnsEmptyPolicy()
    {
        var result = await PolicySourceLoader.LoadPoliciesFromSourcesAsync(
            [],
            Path.GetTempPath(),
            debug: false,
            CancellationToken.None);

        Assert.Null(result.Commands);
        Assert.Null(result.Aliases);
    }
}
