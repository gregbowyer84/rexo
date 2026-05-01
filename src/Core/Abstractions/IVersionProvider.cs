namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface IVersionProvider
{
    Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken);
}
