namespace Rexo.Configuration.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record RepoConfig(
    string Name,
    Dictionary<string, RepoCommandConfig>? Commands,
    Dictionary<string, string>? Aliases)
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public string? SchemaVersion { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public List<string>? Extends { get; init; }
    public RepoVersioningConfig? Versioning { get; init; }
    public List<RepoArtifactConfig>? Artifacts { get; init; }
    public RepoRuntimeConfig? Runtime { get; init; }

    /// <summary>Output path contract resolved by Rexo. Defaults are applied when omitted.</summary>
    public RepoOutputsConfig? Outputs { get; init; }

    /// <summary>Toolchain-specific settings available to policy commands via <c>{{settings.*}}</c>.</summary>
    public Dictionary<string, JsonElement>? Settings { get; init; }

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

    /// <summary>
    /// Controls layered command composition behavior for this command definition.
    /// Allowed values: <c>layer</c>, <c>replace</c>, <c>append</c>, <c>prepend</c>, <c>wrap</c>.
    /// </summary>
    public string? Merge { get; init; }

    /// <summary>Maximum number of steps to run concurrently within a parallel group.</summary>
    public int? MaxParallel { get; init; }
}

public sealed record RepoArgConfig(
    bool Required,
    string? Description = null);

public sealed record RepoOptionConfig(
    string Type,
    JsonElement? Default = null,
    string[]? Allowed = null);

public sealed record RepoStepConfig(
    string? Id = null,
    string? Command = null,
    string? Uses = null,
    string? Run = null,
    string? When = null,
    Dictionary<string, string>? With = null,
    string? Description = null,
    bool? WhenExists = null,
    bool? ContinueOnError = null,
    bool? Parallel = null,
    string[]? DependsOn = null,
    string? OutputPattern = null,
    string? OutputFile = null,
    Dictionary<string, string[]>? Outputs = null);

public sealed record RepoVersioningConfig(
    string Provider,
    string? Fallback = null,
    Dictionary<string, string>? Settings = null);

public sealed record RepoArtifactConfig(
    string Type,
    string? Name = null,
    Dictionary<string, JsonElement>? Settings = null);

public sealed record RepoOutputsConfig
{
    /// <summary>When false, Rexo does not collect or write any output files (manifests, step outputs). Default: <c>true</c>.</summary>
    public bool? Emit { get; init; }

    /// <summary>Root artifacts directory. Default: <c>artifacts</c>.</summary>
    public string? Root { get; init; }

    /// <summary>Test output paths.</summary>
    public RepoTestOutputPathsConfig? Tests { get; init; }

    /// <summary>Analysis output paths.</summary>
    public RepoAnalysisOutputPathsConfig? Analysis { get; init; }

    /// <summary>Security output paths.</summary>
    public RepoSecurityOutputPathsConfig? Security { get; init; }

    /// <summary>Package output directory. Default: <c>artifacts/packages</c>.</summary>
    public string? Packages { get; init; }

    /// <summary>Run manifest output directory. Default: <c>artifacts/manifests</c>.</summary>
    public string? Manifests { get; init; }

    /// <summary>Log output directory. Default: <c>artifacts/logs</c>.</summary>
    public string? Logs { get; init; }

    /// <summary>Temporary scratch directory. Default: <c>.rexo/temp</c>.</summary>
    public string? Temp { get; init; }
}

public sealed record RepoTestOutputPathsConfig
{
    /// <summary>Test result files directory. Default: <c>artifacts/tests</c>.</summary>
    public string? Results { get; init; }

    /// <summary>Coverage output directory. Default: <c>artifacts/coverage</c>.</summary>
    public string? Coverage { get; init; }

    /// <summary>Test report output directory. Default: <c>artifacts/tests/reports</c>.</summary>
    public string? Reports { get; init; }
}

public sealed record RepoAnalysisOutputPathsConfig
{
    /// <summary>Analysis report output directory. Default: <c>artifacts/analysis</c>.</summary>
    public string? Reports { get; init; }

    /// <summary>SARIF output file path. Default: <c>artifacts/analysis/build.sarif</c>.</summary>
    public string? Sarif { get; init; }

}

public sealed record RepoSecurityOutputPathsConfig
{
    /// <summary>Full file path for the npm/security audit JSON output. Default: <c>artifacts/security/audit.json</c>.</summary>
    public string? Audit { get; init; }
}

public sealed record RepoRuntimeConfig(
    RepoPushConfig? Push = null);

public sealed record RepoPushConfig(
    bool? Enabled = null,
    bool? NoPushInPullRequest = null,
    bool? RequireCleanWorkingTree = null,
    string[]? Branches = null);

/// <summary>
/// A partial config document used to inject commands and aliases from a policy file
/// (e.g. <c>policy.json</c>) after policy schema validation.
/// </summary>
public sealed record PolicyConfig(
    Dictionary<string, RepoCommandConfig>? Commands = null,
    Dictionary<string, string>? Aliases = null);
