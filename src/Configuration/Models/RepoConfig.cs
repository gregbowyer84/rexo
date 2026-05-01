namespace Rexo.Configuration.Models;

using System.Text.Json.Serialization;

public sealed record RepoConfig(
    string Name,
    Dictionary<string, RepoCommandConfig> Commands,
    Dictionary<string, string> Aliases)
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public string? SchemaVersion { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public List<string>? Extends { get; init; }
    public RepoVersioningConfig? Versioning { get; init; }
    public List<RepoArtifactConfig>? Artifacts { get; init; }
    public RepoTestsConfig? Tests { get; init; }
    public RepoAnalysisConfig? Analysis { get; init; }
    public string? PushRulesJson { get; init; }

    /// <summary>
    /// Controls how list fields are merged when combining configs via <c>extends</c>.
    /// <list type="bullet">
    ///   <item><c>union</c> (default) — child list entries are appended after base entries.</item>
    ///   <item><c>replace</c> — child list replaces the base list entirely; base entries are discarded.</item>
    ///   <item><c>prepend</c> — child list entries are inserted before base entries.</item>
    /// </list>
    /// </summary>
    public string? MergeStrategy { get; init; }
}

public sealed record RepoCommandConfig(
    string? Description,
    Dictionary<string, RepoOptionConfig> Options,
    List<RepoStepConfig> Steps)
{
    public Dictionary<string, RepoArgConfig>? Args { get; init; }

    /// <summary>Maximum number of steps to run concurrently within a parallel group.</summary>
    public int? MaxParallel { get; init; }
}

public sealed record RepoArgConfig(
    bool Required,
    string? Description = null);

public sealed record RepoOptionConfig(
    string Type,
    string? Default = null,
    string[]? Allowed = null);

public sealed record RepoStepConfig(
    string? Id = null,
    string? Command = null,
    string? Uses = null,
    string? Run = null,
    string? When = null,
    string? Description = null,
    bool? ContinueOnError = null,
    bool? Parallel = null,
    string[]? DependsOn = null,
    string? OutputPattern = null,
    string? OutputFile = null);

public sealed record RepoVersioningConfig(
    string Provider,
    string? Fallback = null,
    Dictionary<string, string>? Settings = null);

public sealed record RepoArtifactConfig(
    string Type,
    string Name,
    Dictionary<string, string>? Settings = null);

public sealed record RepoTestsConfig(
    bool Enabled,
    string[]? Projects = null,
    string Configuration = "Release",
    string? ResultsOutput = null,
    string? CoverageOutput = null,
    bool? CollectCoverage = null,
    int? CoverageThreshold = null);

public sealed record RepoAnalysisConfig(
    bool Enabled,
    bool FailOnIssues = true,
    string[]? Tools = null,
    string? Configuration = null);

/// <summary>
/// A partial config document used to inject commands and aliases from a policy file
/// (e.g. <c>policy.json</c>). Does not require <c>$schema</c> or <c>schemaVersion</c>.
/// </summary>
public sealed record PolicyConfig(
    Dictionary<string, RepoCommandConfig>? Commands = null,
    Dictionary<string, string>? Aliases = null);
