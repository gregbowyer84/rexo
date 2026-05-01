namespace Rexo.Core.Models;

public sealed record ExecutionContext(
    string RepositoryRoot,
    string? Branch,
    string? CommitSha,
    IReadOnlyDictionary<string, object?> Values)
{
    public string? ShortSha { get; init; }
    public string? RemoteUrl { get; init; }
    public bool IsCi { get; init; }
    public string? CiProvider { get; init; }
    public bool IsPullRequest { get; init; }
    public bool IsCleanWorkingTree { get; init; }
    public IReadOnlyDictionary<string, string> Args { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string?> Options { get; init; } = new Dictionary<string, string?>();
    public VersionResult? Version { get; init; }
    public IReadOnlyDictionary<string, StepResult> CompletedSteps { get; init; } = new Dictionary<string, StepResult>();

    public static ExecutionContext Empty(string repositoryRoot) =>
        new(repositoryRoot, null, null, new Dictionary<string, object?>());

    public ExecutionContext WithStep(StepResult result) =>
        string.IsNullOrEmpty(result.StepId)
            ? this
            : this with
            {
                CompletedSteps = new Dictionary<string, StepResult>(CompletedSteps) { [result.StepId] = result }
            };

    public ExecutionContext WithVersion(VersionResult version) =>
        this with { Version = version };
}
