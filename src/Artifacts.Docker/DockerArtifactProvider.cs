namespace Rexo.Artifacts.Docker;

using System.Diagnostics;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class DockerArtifactProvider : IArtifactProvider
{
    public string Type => "docker";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var image = GetSetting(artifact, "image") ?? artifact.Name;
        var dockerfile = GetSetting(artifact, "dockerfile") ?? "Dockerfile";
        var buildContext = GetSetting(artifact, "context") ?? ".";

        var tag = context.Version is not null ? $"{image}:{context.Version.SemVer}" : image;
        var args = $"build -f {dockerfile} -t {tag} {buildContext}";

        Console.WriteLine($"  > docker {args}");

        var result = await RunDockerAsync(args, context.RepositoryRoot, cancellationToken);

        return new ArtifactBuildResult(
            Name: artifact.Name,
            Success: result.ExitCode == 0,
            Location: result.ExitCode == 0 ? tag : null);
    }

    public async Task<ArtifactTagResult> TagAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var image = GetSetting(artifact, "image") ?? artifact.Name;
        var tags = BuildTags(image, artifact, context);
        var appliedTags = new List<string>();

        if (context.Version is null)
        {
            return new ArtifactTagResult(artifact.Name, false, Array.Empty<string>());
        }

        var sourceTag = $"{image}:{context.Version.SemVer}";

        foreach (var tag in tags)
        {
            if (tag == sourceTag) continue;
            var tagArgs = $"tag {sourceTag} {tag}";
            Console.WriteLine($"  > docker {tagArgs}");
            var result = await RunDockerAsync(tagArgs, context.RepositoryRoot, cancellationToken);
            if (result.ExitCode == 0) appliedTags.Add(tag);
        }

        return new ArtifactTagResult(artifact.Name, true, tags);
    }

    public async Task<ArtifactPushResult> PushAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var image = GetSetting(artifact, "image") ?? artifact.Name;
        var tags = BuildTags(image, artifact, context);
        var pushed = new List<string>();

        foreach (var tag in tags)
        {
            var pushArgs = $"push {tag}";
            Console.WriteLine($"  > docker {pushArgs}");
            var result = await RunDockerAsync(pushArgs, context.RepositoryRoot, cancellationToken);
            if (result.ExitCode == 0) pushed.Add(tag);
        }

        return new ArtifactPushResult(artifact.Name, pushed.Count > 0, pushed);
    }

    private static IReadOnlyList<string> BuildTags(
        string image,
        ArtifactConfig artifact,
        ExecutionContext context)
    {
        var tags = new List<string>();
        var tagStrategies = GetSetting(artifact, "tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? new[] { "semver" };

        foreach (var strategy in tagStrategies)
        {
            var tag = strategy.Trim() switch
            {
                "semver" when context.Version is not null => $"{image}:{context.Version.SemVer}",
                "major-minor" when context.Version is not null => $"{image}:{context.Version.Major}.{context.Version.Minor}",
                "major" when context.Version is not null => $"{image}:{context.Version.Major}",
                "branch" when context.Branch is not null => $"{image}:{Slug(context.Branch)}",
                "sha" when context.ShortSha is not null => $"{image}:sha-{context.ShortSha}",
                "latest-on-main" when context.Branch == "main" => $"{image}:latest",
                _ when strategy.StartsWith("{{", StringComparison.Ordinal) => null,
                _ => null,
            };

            if (tag is not null) tags.Add(tag);
        }

        if (tags.Count == 0 && context.Version is not null)
        {
            tags.Add($"{image}:{context.Version.SemVer}");
        }

        return tags;
    }

    private static async Task<(int ExitCode, string Output)> RunDockerAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);

        return (process.ExitCode, stdout + stderr);
    }

    private static string? GetSetting(ArtifactConfig artifact, string key) =>
        artifact.Settings.TryGetValue(key, out var v) ? v : null;

    private static string Slug(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
