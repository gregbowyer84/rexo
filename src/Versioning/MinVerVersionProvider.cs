namespace Rexo.Versioning;

using System.Diagnostics;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

/// <summary>
/// Resolves the version by shelling out to the <c>minver</c> dotnet tool.
/// MinVer outputs a single SemVer 2.0 string (e.g. "1.2.3-alpha.0.1").
/// Falls back to <see cref="FixedVersionProvider"/> if minver is not installed
/// or the working directory is not a git repository.
/// </summary>
public sealed class MinVerVersionProvider : IVersionProvider
{
    public async Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workingDir = context.RepositoryRoot;
        var tagPrefix = config.Settings?.TryGetValue("tagPrefix", out var p) == true ? p : null;
        var minMajor = config.Settings?.TryGetValue("minimumMajorMinor", out var m) == true ? m : null;

        try
        {
            var arguments = "minver";
            if (!string.IsNullOrEmpty(tagPrefix)) arguments += $" --tag-prefix {tagPrefix}";
            if (!string.IsNullOrEmpty(minMajor)) arguments += $" --minimum-major-minor {minMajor}";

            var (exitCode, output) = await RunProcessAsync(
                "dotnet",
                arguments,
                workingDir,
                cancellationToken);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return Fallback(config, context);
            }

            var version = output.Trim();
            return FixedVersionProvider.ParseSemVer(version, context);
        }
        catch (Exception)
        {
            return Fallback(config, context);
        }
    }

    private static async Task<(int exitCode, string output)> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;

        return (process.ExitCode, output);
    }

    private static VersionResult Fallback(VersioningConfig config, ExecutionContext context) =>
        FixedVersionProvider.ParseSemVer(config.Fallback ?? "0.1.0-local", context);
}
