namespace Rexo.Execution.Tests;

using System.Text.Json;
using Rexo.Configuration.Models;
using Rexo.Execution;

/// <summary>
/// Tests for BuildOutputsContext and BuildSettingsContext helper methods.
/// </summary>
public sealed class OutputsAndSettingsContextTests
{
    private static RepoConfig EmptyConfig() =>
        new(Name: "test", Commands: null, Aliases: null);

    // ----------------------------------------------------------------
    // BuildOutputsContext
    // ----------------------------------------------------------------

    [Fact]
    public void BuildOutputsContextNullOutputsUsesDefaultPaths()
    {
        var config = EmptyConfig();

        var ctx = ConfigCommandLoader.BuildOutputsContext(config);

        Assert.Equal("artifacts", ctx["root"]);
        Assert.Equal("artifacts/packages", ctx["packages"]);
        Assert.Equal("artifacts/manifests", ctx["manifests"]);
        Assert.Equal("artifacts/logs", ctx["logs"]);
        Assert.Equal(".rexo/temp", ctx["temp"]);

        var tests = Assert.IsType<Dictionary<string, object?>>(ctx["tests"]);
        Assert.Equal("artifacts/tests", tests["results"]);
        Assert.Equal("artifacts/coverage", tests["coverage"]);
        Assert.Equal("artifacts/tests/reports", tests["reports"]);

        var analysis = Assert.IsType<Dictionary<string, object?>>(ctx["analysis"]);
        Assert.Equal("artifacts/analysis", analysis["reports"]);
        Assert.Equal("artifacts/analysis/build.sarif", analysis["sarif"]);

        var security = Assert.IsType<Dictionary<string, object?>>(ctx["security"]);
        Assert.Equal("artifacts/security/audit.json", security["audit"]);
    }

    [Fact]
    public void BuildOutputsContextCustomRootDerivesPaths()
    {
        var config = EmptyConfig() with
        {
            Outputs = new RepoOutputsConfig { Root = "out" },
        };

        var ctx = ConfigCommandLoader.BuildOutputsContext(config);

        Assert.Equal("out", ctx["root"]);
        Assert.Equal("out/packages", ctx["packages"]);

        var tests = Assert.IsType<Dictionary<string, object?>>(ctx["tests"]);
        Assert.Equal("out/tests", tests["results"]);
        Assert.Equal("out/coverage", tests["coverage"]);
    }

    [Fact]
    public void BuildOutputsContextCustomTestPathsOverridesDefaults()
    {
        var config = EmptyConfig() with
        {
            Outputs = new RepoOutputsConfig
            {
                Tests = new RepoTestOutputPathsConfig
                {
                    Results = "custom/test-results",
                    Coverage = "custom/cov",
                },
            },
        };

        var ctx = ConfigCommandLoader.BuildOutputsContext(config);

        var tests = Assert.IsType<Dictionary<string, object?>>(ctx["tests"]);
        Assert.Equal("custom/test-results", tests["results"]);
        Assert.Equal("custom/cov", tests["coverage"]);
    }

    [Fact]
    public void BuildOutputsContextCustomAnalysisPathsOverridesDefaults()
    {
        var config = EmptyConfig() with
        {
            Outputs = new RepoOutputsConfig
            {
                Analysis = new RepoAnalysisOutputPathsConfig
                {
                    Reports = "custom/reports",
                    Sarif = "custom/sarif",
                },
            },
        };

        var ctx = ConfigCommandLoader.BuildOutputsContext(config);

        var analysis = Assert.IsType<Dictionary<string, object?>>(ctx["analysis"]);
        Assert.Equal("custom/reports", analysis["reports"]);
        Assert.Equal("custom/sarif", analysis["sarif"]);
    }

    // ----------------------------------------------------------------
    // BuildSettingsContext
    // ----------------------------------------------------------------

    [Fact]
    public void BuildSettingsContextNullSettingsReturnsEmptyDict()
    {
        var config = EmptyConfig();

        var ctx = ConfigCommandLoader.BuildSettingsContext(config);

        Assert.Empty(ctx);
    }

    [Fact]
    public void BuildSettingsContextFlatStringValuesRoundTrips()
    {
        var json = JsonDocument.Parse("""{"configuration":"Release","solution":"my.slnx"}""");
        var config = EmptyConfig() with
        {
            Settings = new Dictionary<string, JsonElement>
            {
                ["dotnet"] = json.RootElement,
            },
        };

        var ctx = ConfigCommandLoader.BuildSettingsContext(config);

        var dotnet = Assert.IsType<Dictionary<string, object?>>(ctx["dotnet"]);
        Assert.Equal("Release", dotnet["configuration"]);
        Assert.Equal("my.slnx", dotnet["solution"]);
    }

    [Fact]
    public void BuildSettingsContextNestedObjectResolvesNestedKey()
    {
        var json = JsonDocument.Parse("""{"packageManager":"pnpm","auditLevel":"moderate"}""");
        var config = EmptyConfig() with
        {
            Settings = new Dictionary<string, JsonElement>
            {
                ["node"] = json.RootElement,
            },
        };

        var ctx = ConfigCommandLoader.BuildSettingsContext(config);

        var node = Assert.IsType<Dictionary<string, object?>>(ctx["node"]);
        Assert.Equal("pnpm", node["packageManager"]);
        Assert.Equal("moderate", node["auditLevel"]);
    }

    [Fact]
    public void BuildSettingsContextBooleanValuesSerializedAsStrings()
    {
        var json = JsonDocument.Parse("""{"enabled":true,"disabled":false}""");
        var config = EmptyConfig() with
        {
            Settings = new Dictionary<string, JsonElement>
            {
                ["flags"] = json.RootElement,
            },
        };

        var ctx = ConfigCommandLoader.BuildSettingsContext(config);

        var flags = Assert.IsType<Dictionary<string, object?>>(ctx["flags"]);
        Assert.Equal("true", flags["enabled"]);
        Assert.Equal("false", flags["disabled"]);
    }
}
