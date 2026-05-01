namespace Rexo.Execution.Tests;

using Rexo.Core.Models;
using Rexo.Templating;

public sealed class TemplateRendererTests
{
    private static ExecutionContext MakeContext(
        string? version = null,
        Dictionary<string, string>? args = null,
        Dictionary<string, string?>? options = null)
    {
        var ctx = ExecutionContext.Empty("C:\\repo");
        if (args is not null || options is not null)
        {
            ctx = ctx with
            {
                Args = args ?? new Dictionary<string, string>(),
                Options = options ?? new Dictionary<string, string?>(),
            };
        }

        if (version is not null)
        {
            var parts = version.Split('.');
            var major = parts.Length > 0 && int.TryParse(parts[0], out var mj) ? mj : 0;
            var minor = parts.Length > 1 && int.TryParse(parts[1], out var mn) ? mn : 0;
            var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
            var vr = new VersionResult(version, major, minor, patch, null, "abc1234", "abc1234", false, true);
            ctx = ctx.WithVersion(vr);
        }

        return ctx;
    }

    [Fact]
    public void RenderReturnsPlainTextUnchanged()
    {
        var renderer = new TemplateRenderer();
        var result = renderer.Render("hello world", MakeContext());
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void RenderResolvesArgVariable()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(args: new Dictionary<string, string> { ["name"] = "acme" });
        var result = renderer.Render("hello {{args.name}}", ctx);
        Assert.Equal("hello acme", result);
    }

    [Fact]
    public void RenderResolvesOptionVariable()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(options: new Dictionary<string, string?> { ["env"] = "prod" });
        var result = renderer.Render("env={{options.env}}", ctx);
        Assert.Equal("env=prod", result);
    }

    [Fact]
    public void RenderAppliesSlugFilter()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(args: new Dictionary<string, string> { ["branch"] = "Feature/My Cool Branch" });
        var result = renderer.Render("{{args.branch | slug}}", ctx);
        Assert.Equal("feature-my-cool-branch", result);
    }

    [Fact]
    public void RenderAppliesUpperFilter()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(args: new Dictionary<string, string> { ["env"] = "prod" });
        var result = renderer.Render("{{args.env | upper}}", ctx);
        Assert.Equal("PROD", result);
    }

    [Fact]
    public void RenderAppliesLowerFilter()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(args: new Dictionary<string, string> { ["tag"] = "LATEST" });
        var result = renderer.Render("{{args.tag | lower}}", ctx);
        Assert.Equal("latest", result);
    }

    [Fact]
    public void RenderAppliesDefaultFilter()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext();
        var result = renderer.Render("{{args.missing | default('fallback')}}", ctx);
        Assert.Equal("fallback", result);
    }

    [Fact]
    public void RenderResolvesVersionMajorMinorPatch()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(version: "2.3.4");
        Assert.Equal("2", renderer.Render("{{version.major}}", ctx));
        Assert.Equal("3", renderer.Render("{{version.minor}}", ctx));
        Assert.Equal("4", renderer.Render("{{version.patch}}", ctx));
    }

    [Fact]
    public void RenderLeavesMissingVariableAsEmpty()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext();
        var result = renderer.Render("x={{args.notexist}}", ctx);
        Assert.Equal("x=", result);
    }

    [Fact]
    public void RenderHandlesMultipleSubstitutionsInOneString()
    {
        var renderer = new TemplateRenderer();
        var ctx = MakeContext(
            args: new Dictionary<string, string> { ["a"] = "hello", ["b"] = "world" });
        var result = renderer.Render("{{args.a}} {{args.b}}", ctx);
        Assert.Equal("hello world", result);
    }
}
