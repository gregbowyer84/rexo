namespace Rexo.Versioning;

using System.Diagnostics;
using System.Text.Json;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

/// <summary>
/// Resolves the version by shelling out to the <c>nbgv</c> (Nerdbank.GitVersioning) CLI tool.
/// Runs <c>nbgv get-version -f json</c> and parses the JSON output.
/// Falls back to <see cref="FixedVersionProvider"/> if nbgv is not installed
/// or the working directory is not a git repository.
/// </summary>
public sealed class NbgvVersionProvider : IVersionProvider
{
    public async Task<VersionResult> ResolveAsync(
        VersioningConfig config,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workingDir = context.RepositoryRoot;

        try
        {
            var (exitCode, output) = await RunProcessAsync(
                "nbgv",
                "get-version -f json",
                workingDir,
                cancellationToken);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return Fallback(config, context);
            }

            return ParseNbgvOutput(output, context) ?? Fallback(config, context);
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

    private static VersionResult? ParseNbgvOutput(string json, ExecutionContext context)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // nbgv output: { "Version": "1.2.3", "SemVer2": "1.2.3-alpha.0.1", ... }
            var semVer = TryGetString(root, "SemVer2") ?? TryGetString(root, "Version") ?? "0.1.0";
            var versionStr = TryGetString(root, "Version") ?? semVer;

            // Parse major/minor/patch from Version field (e.g. "1.2.3.0")
            var vparts = versionStr.Split('.');
            var major = vparts.Length > 0 && int.TryParse(vparts[0], out var maj) ? maj : 0;
            var minor = vparts.Length > 1 && int.TryParse(vparts[1], out var min) ? min : 0;
            var patch = vparts.Length > 2 && int.TryParse(vparts[2], out var pat) ? pat : 0;

            var sha = TryGetString(root, "GitCommitId") ?? context.CommitSha ?? "unknown";
            var shortSha = sha.Length >= 7 ? sha[..7] : (context.ShortSha ?? "unknown");

            // Determine prerelease from SemVer2
            var dashIndex = semVer.IndexOf('-', StringComparison.Ordinal);
            var prerelease = dashIndex >= 0 ? semVer[(dashIndex + 1)..] : null;
            var isPrerelease = !string.IsNullOrEmpty(prerelease);

            return new VersionResult(
                SemVer: semVer,
                Major: major,
                Minor: minor,
                Patch: patch,
                PreRelease: prerelease,
                CommitSha: sha,
                ShortSha: shortSha,
                IsPreRelease: isPrerelease,
                IsStable: !isPrerelease);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static VersionResult Fallback(VersioningConfig config, ExecutionContext context) =>
        FixedVersionProvider.ParseSemVer(config.Fallback ?? "0.1.0-local", context);
}
