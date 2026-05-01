namespace Rexo.Versioning;

using System.Diagnostics;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

/// <summary>
/// Resolves the version by reading the most recent annotated or lightweight git tag
/// reachable from HEAD via <c>git describe --tags --abbrev=0</c>.
/// Tags are expected to be SemVer 2.0 with an optional leading <c>v</c> (e.g. <c>v1.2.3</c>).
/// Falls back to <see cref="FixedVersionProvider"/> if the working directory is not a git
/// repository or no tags are reachable.
/// </summary>
public sealed class GitTagVersionProvider : IVersionProvider
{
    public async Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "describe --tags --abbrev=0")
            {
                WorkingDirectory = context.RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start git process.");

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return Fallback(config, context);
            }

            var tag = output.Trim();

            // Strip leading 'v' or 'V' prefix (e.g. v1.2.3 → 1.2.3)
            if (tag.Length > 1 && (tag[0] == 'v' || tag[0] == 'V'))
            {
                tag = tag[1..];
            }

            return FixedVersionProvider.ParseSemVer(tag, context);
        }
        catch (Exception)
        {
            return Fallback(config, context);
        }
    }

    private static VersionResult Fallback(VersioningConfig config, ExecutionContext context)
    {
        var fallback = config.Fallback ?? "0.1.0";
        return FixedVersionProvider.ParseSemVer(fallback, context);
    }
}
