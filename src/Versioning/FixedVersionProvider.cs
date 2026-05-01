namespace Rexo.Versioning;

using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class FixedVersionProvider : IVersionProvider
{
    public Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var version = config.Settings?.TryGetValue("version", out var v) == true
            ? v
            : config.Fallback ?? "0.1.0";

        return Task.FromResult(ParseSemVer(version ?? "0.1.0", context));
    }

    internal static VersionResult ParseSemVer(string version, ExecutionContext context)
    {
        var dashIndex = version.IndexOf('-', StringComparison.Ordinal);
        var numericPart = dashIndex >= 0 ? version[..dashIndex] : version;
        var prerelease = dashIndex >= 0 ? version[(dashIndex + 1)..] : null;

        var parts = numericPart.Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], out var maj) ? maj : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var min) ? min : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var pat) ? pat : 0;

        var isPrerelease = !string.IsNullOrEmpty(prerelease);

        return new VersionResult(
            SemVer: version,
            Major: major,
            Minor: minor,
            Patch: patch,
            PreRelease: prerelease,
            CommitSha: context.CommitSha ?? "unknown",
            ShortSha: context.ShortSha ?? "unknown",
            IsPreRelease: isPrerelease,
            IsStable: !isPrerelease)
        {
            Branch = context.Branch,
            NuGetVersion = prerelease is not null
                ? $"{major}.{minor}.{patch}-{prerelease.Replace('+', '.')}"
                : version,
            DockerVersion = version.Replace('+', '-'),
        };
    }
}
