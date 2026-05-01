namespace Rexo.Core.Models;

public sealed record RunManifest
{
    public string SchemaVersion { get; init; } = "1.0";
    public string? ToolVersion { get; init; }
    public string? RepoName { get; init; }
    public string? RepoRoot { get; init; }
    public string? Branch { get; init; }
    public string? CommitSha { get; init; }
    public string? RemoteUrl { get; init; }
    public string? CiProvider { get; init; }
    public bool IsCi { get; init; }
    public string? CommandExecuted { get; init; }
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public VersionResult? Version { get; init; }
    public IReadOnlyList<StepManifestEntry> Steps { get; init; } = Array.Empty<StepManifestEntry>();
    public IReadOnlyList<ArtifactManifestEntry> Artifacts { get; init; } = Array.Empty<ArtifactManifestEntry>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record StepManifestEntry(
    string StepId,
    bool Success,
    int ExitCode,
    double DurationMs);

public sealed record ArtifactManifestEntry(
    string Type,
    string Name,
    bool Built,
    bool Pushed,
    IReadOnlyList<string> Tags);
