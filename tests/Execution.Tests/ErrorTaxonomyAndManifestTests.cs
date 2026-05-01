namespace Rexo.Execution.Tests;

using Rexo.Core.Models;

/// <summary>
/// Tests covering the structured error taxonomy, suggestion engine, version result
/// extended fields, and run manifest completeness.
/// </summary>
public sealed class ErrorTaxonomyAndManifestTests
{
    // ── RexoError ───────────────────────────────────────────────────────────

    [Fact]
    public void RexoErrorToStringIncludesCodeAndMessage()
    {
        var err = new RexoError(ErrorCodes.CommandNotFound, "Command 'foobar' was not found.");
        Assert.Contains("CMD-001", err.ToString());
        Assert.Contains("foobar", err.ToString());
    }

    [Fact]
    public void RexoErrorToStringIncludesSuggestedFixWhenSet()
    {
        var err = new RexoError(ErrorCodes.CommandNotFound, "Not found.")
        { SuggestedFix = "Try 'rx list'" };
        Assert.Contains("Try 'rx list'", err.ToString());
    }

    [Fact]
    public void RexoErrorToStringOmitsSuggestedFixWhenNull()
    {
        var err = new RexoError(ErrorCodes.StepFailed, "Step blew up.");
        Assert.DoesNotContain("→", err.ToString());
    }

    [Fact]
    public void ErrorCodesAreNonEmpty()
    {
        Assert.NotEmpty(ErrorCodes.CommandNotFound);
        Assert.NotEmpty(ErrorCodes.ConfigNotFound);
        Assert.NotEmpty(ErrorCodes.StepFailed);
        Assert.NotEmpty(ErrorCodes.VersionResolutionFailed);
        Assert.NotEmpty(ErrorCodes.ArtifactPushFailed);
        Assert.NotEmpty(ErrorCodes.PolicyViolation);
    }

    // ── CommandResult.FailWithError ─────────────────────────────────────────

    [Fact]
    public void FailWithErrorPopulatesStructuredErrors()
    {
        var error = new RexoError(ErrorCodes.CommandNotFound, "Not found.");
        var result = CommandResult.FailWithError("foo", 8, error);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        Assert.Single(result.StructuredErrors);
        Assert.Equal(ErrorCodes.CommandNotFound, result.StructuredErrors[0].Code);
    }

    [Fact]
    public void FailWithErrorMessageMatchesErrorMessage()
    {
        var error = new RexoError(ErrorCodes.ConfigNotFound, "Config missing.");
        var result = CommandResult.FailWithError("build", 1, error);
        Assert.Equal("Config missing.", result.Message);
    }

    // ── Suggestion engine (DefaultCommandExecutor) ──────────────────────────

    [Fact]
    public async Task UnknownCommandReturnsStructuredError()
    {
        var registry = new CommandRegistry();
        registry.Register("build", (_, _) => Task.FromResult(CommandResult.Ok("build")));

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync(
            "buuld",
            new CommandInvocation(new Dictionary<string, string>(), new Dictionary<string, string?>(), false, null, "."),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(8, result.ExitCode);
        Assert.NotEmpty(result.StructuredErrors);
        Assert.Equal(ErrorCodes.CommandNotFound, result.StructuredErrors[0].Code);
    }

    [Fact]
    public async Task CloselyMisspelledCommandSuggestsCorrection()
    {
        var registry = new CommandRegistry();
        registry.Register("deploy", (_, _) => Task.FromResult(CommandResult.Ok("deploy")));

        var executor = new DefaultCommandExecutor(registry);
        var result = await executor.ExecuteAsync(
            "depoly",
            new CommandInvocation(new Dictionary<string, string>(), new Dictionary<string, string?>(), false, null, "."),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("deploy", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ── VersionResult extended fields ────────────────────────────────────────

    [Fact]
    public void VersionResultAssemblyVersionHasFourParts()
    {
        var vr = new VersionResult("1.2.3", 1, 2, 3, null, "abc", "abc", false, true);
        Assert.Equal("1.2.3.0", vr.AssemblyVersion);
    }

    [Fact]
    public void VersionResultFileVersionHasFourParts()
    {
        var vr = new VersionResult("2.0.0", 2, 0, 0, null, "abc", "abc", false, true);
        Assert.Equal("2.0.0.0", vr.FileVersion);
    }

    [Fact]
    public void VersionResultInformationalVersionIncludesBuildMetadata()
    {
        var vr = new VersionResult("1.0.0", 1, 0, 0, null, "abc", "abc", false, true)
        { BuildMetadata = "20250101.1" };
        Assert.Equal("1.0.0+20250101.1", vr.InformationalVersion);
    }

    [Fact]
    public void VersionResultInformationalVersionWithoutBuildMetadataEqualsSemVer()
    {
        var vr = new VersionResult("3.1.4-beta.1", 3, 1, 4, "beta.1", "abc", "abc", true, false);
        Assert.Equal("3.1.4-beta.1", vr.InformationalVersion);
    }

    [Fact]
    public void VersionResultBranchCanBeSet()
    {
        var vr = new VersionResult("1.0.0", 1, 0, 0, null, "sha", "sha", false, true)
        { Branch = "main" };
        Assert.Equal("main", vr.Branch);
    }

    [Fact]
    public void VersionResultCommitsSinceVersionSourceCanBeSet()
    {
        var vr = new VersionResult("1.0.0", 1, 0, 0, null, "sha", "sha", false, true)
        { CommitsSinceVersionSource = 5 };
        Assert.Equal(5, vr.CommitsSinceVersionSource);
    }

    // ── RunManifest completeness ─────────────────────────────────────────────

    [Fact]
    public void RunManifestDurationDerivedFromStartAndComplete()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddSeconds(10);
        var manifest = new RunManifest { StartedAt = start, CompletedAt = end };
        Assert.Equal(TimeSpan.FromSeconds(10), manifest.Duration);
    }

    [Fact]
    public void RunManifestConfigHashCanBeSet()
    {
        var manifest = new RunManifest { ConfigHash = "abc123" };
        Assert.Equal("abc123", manifest.ConfigHash);
    }

    [Fact]
    public void RunManifestAssemblyVersionCanBeSet()
    {
        var manifest = new RunManifest { AssemblyVersion = "2.1.0.0" };
        Assert.Equal("2.1.0.0", manifest.AssemblyVersion);
    }

    [Fact]
    public void RunManifestNuGetVersionCanBeSet()
    {
        var manifest = new RunManifest { NuGetVersion = "2.1.0-beta.1" };
        Assert.Equal("2.1.0-beta.1", manifest.NuGetVersion);
    }

    [Fact]
    public void RunManifestDefaultSchemaVersionIs10()
    {
        var manifest = new RunManifest();
        Assert.Equal("1.0", manifest.SchemaVersion);
    }
}
