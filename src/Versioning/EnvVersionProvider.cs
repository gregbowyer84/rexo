namespace Rexo.Versioning;

using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class EnvVersionProvider : IVersionProvider
{
    public Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var envVar = config.Settings?.TryGetValue("variable", out var v) == true
            ? v ?? "VERSION"
            : "VERSION";

        var version = context.GetEnvironmentValue(envVar)
            ?? config.Fallback
            ?? "0.1.0-local";

        return new FixedVersionProvider().ResolveAsync(
            new VersioningConfig("fixed", version),
            context,
            cancellationToken);
    }
}
