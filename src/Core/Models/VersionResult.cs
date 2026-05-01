namespace Rexo.Core.Models;

public sealed record VersionResult(
    string SemVer,
    int Major,
    int Minor,
    int Patch,
    string? PreRelease,
    string CommitSha,
    string ShortSha,
    bool IsPreRelease,
    bool IsStable)
{
    /// <summary>Build metadata segment (e.g. the part after <c>+</c> in SemVer 2.0).</summary>
    public string? BuildMetadata { get; init; }

    /// <summary>Repository branch at the time of version resolution.</summary>
    public string? Branch { get; init; }

    /// <summary>Version string for NuGet packages (pre-release separators converted to dots).</summary>
    public string? NuGetVersion { get; init; }

    /// <summary>Version string for Docker image tags (only alphanumeric, dot, hyphen).</summary>
    public string? DockerVersion { get; init; }

    /// <summary>Assembly-compatible version (Major.Minor.Patch.0).</summary>
    public string AssemblyVersion =>
        $"{Major}.{Minor}.{Patch}.0";

    /// <summary>File version (Major.Minor.Patch.0).</summary>
    public string FileVersion =>
        $"{Major}.{Minor}.{Patch}.0";

    /// <summary>Full informational version string including pre-release and build metadata.</summary>
    public string InformationalVersion =>
        BuildMetadata is not null ? $"{SemVer}+{BuildMetadata}" : SemVer;

    /// <summary>Number of commits since the version source tag (if available).</summary>
    public int? CommitsSinceVersionSource { get; init; }

    /// <summary>
    /// Numeric weight of the pre-release label for sorting purposes.
    /// Conventional mapping: alpha=1, beta=2, rc=3, preview=4; null when stable (no pre-release label).
    /// </summary>
    public int? WeightedPreReleaseNumber =>
        string.IsNullOrEmpty(PreRelease) ? null :
        PreRelease.StartsWith("alpha", StringComparison.OrdinalIgnoreCase) ? 1 :
        PreRelease.StartsWith("beta", StringComparison.OrdinalIgnoreCase) ? 2 :
        PreRelease.StartsWith("rc", StringComparison.OrdinalIgnoreCase) ? 3 :
        PreRelease.StartsWith("preview", StringComparison.OrdinalIgnoreCase) ? 4 :
        0;
}
