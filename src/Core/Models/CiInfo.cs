namespace Rexo.Core.Models;

public sealed record CiInfo(
    bool IsCi,
    string? Provider,
    string? BuildId,
    string? Branch,
    bool IsPullRequest)
{
    /// <summary>Human-readable build/run number (e.g. GITHUB_RUN_NUMBER, BUILD_BUILDNUMBER).</summary>
    public string? RunNumber { get; init; }

    /// <summary>Name of the workflow, pipeline, or job definition.</summary>
    public string? WorkflowName { get; init; }

    /// <summary>Name of the current job within the workflow.</summary>
    public string? JobName { get; init; }

    /// <summary>The actor (user or bot) that triggered the run.</summary>
    public string? Actor { get; init; }

    /// <summary>Git tag associated with the triggering ref, if any.</summary>
    public string? Tag { get; init; }

    /// <summary>URL to the build/pipeline run in the CI provider's web UI.</summary>
    public string? BuildUrl { get; init; }

    /// <summary>Number of times this run has been retried/re-attempted.</summary>
    public string? RunAttempt { get; init; }
}
