namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface IArtifactProvider
{
    string Type { get; }

    Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken);

    Task<ArtifactTagResult> TagAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken);

    Task<ArtifactPushResult> PushAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken);

    IReadOnlyList<string> GetPlannedTags(ArtifactConfig artifact, ExecutionContext context) =>
        Array.Empty<string>();
}
