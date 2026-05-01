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
    bool IsStable);
