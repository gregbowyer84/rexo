namespace Rexo.Core.Models;

public sealed record ArtifactConfig(
    string Type,
    string Name,
    IReadOnlyDictionary<string, string> Settings);

public sealed record ArtifactBuildResult(string Name, bool Success, string? Location);

public sealed record ArtifactTagResult(string Name, bool Success, IReadOnlyList<string> Tags);

public sealed record ArtifactPushResult(string Name, bool Success, IReadOnlyList<string> PublishedReferences);
