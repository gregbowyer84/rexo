namespace Rexo.Versioning;

using System.Diagnostics;
using System.Text.Json;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

/// <summary>
/// Resolves the version by shelling out to the <c>gitversion</c> CLI tool.
/// Falls back to <see cref="FixedVersionProvider"/> if gitversion is not installed
/// or the working directory is not a git repository.
/// </summary>
public sealed class GitVersionVersionProvider : IVersionProvider
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
                "gitversion",
                "/output json",
                workingDir,
                cancellationToken);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return FallbackVersion(config, context);
            }

            return ParseGitVersionOutput(output, context) ?? FallbackVersion(config, context);
        }
        catch (Exception)
        {
            return FallbackVersion(config, context);
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

    private static VersionResult? ParseGitVersionOutput(string json, ExecutionContext context)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var semVer = TryGetString(root, "SemVer") ?? TryGetString(root, "FullSemVer") ?? "0.1.0";
            var major = TryGetInt(root, "Major") ?? 0;
            var minor = TryGetInt(root, "Minor") ?? 0;
            var patch = TryGetInt(root, "Patch") ?? 0;
            var prerelease = TryGetString(root, "PreReleaseTag");
            var sha = TryGetString(root, "Sha") ?? context.CommitSha ?? "unknown";
            var shortSha = TryGetString(root, "ShortSha") ?? context.ShortSha ?? "unknown";

            var isPrerelease = !string.IsNullOrEmpty(prerelease);

            return new VersionResult(
                SemVer: semVer,
                Major: major,
                Minor: minor,
                Patch: patch,
                PreRelease: string.IsNullOrEmpty(prerelease) ? null : prerelease,
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

    private static int? TryGetInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
            {
                return value;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static VersionResult FallbackVersion(VersioningConfig config, ExecutionContext context) =>
        FixedVersionProvider.ParseSemVer(config.Fallback ?? "0.1.0-local", context);
}
