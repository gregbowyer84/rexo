namespace Rexo.Artifacts.Helm;

using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;

/// <summary>
/// Helm (non-OCI / ChartMuseum) artifact provider.
/// Build: <c>helm package</c>.  Push: <c>helm cm-push</c> for http/https repos,
/// or file copy for filesystem destinations.  Type key: <c>helm</c>.
/// </summary>
public sealed class HelmArtifactProvider : IArtifactProvider
{
    public static void Register(ArtifactProviderRegistry registry) =>
        registry.Register("helm", new HelmArtifactProvider());

    private const string DefaultContainerImage = "alpine/helm:3.17.3";

    public string Type => "helm";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workDir = context.RepositoryRoot;
        var dockerImage = ResolveDockerImage(artifact);
        var chartDir = GetSetting(artifact, "chart-directory") ?? ".";
        var outputDir = GetSetting(artifact, "output-directory") ?? workDir;

        var args = new List<string> { "package", chartDir, "--destination", outputDir };
        if (context.Version is not null)
        {
            args.Add("--version");
            args.Add(context.Version.SemVer);
            args.Add("--app-version");
            args.Add(context.Version.SemVer);
        }

        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-build-args")));

        Console.WriteLine($"  > helm {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("helm", args, workDir, artifact, dockerImage, cancellationToken);

        return new ArtifactBuildResult(artifact.Name, result.ExitCode == 0, result.ExitCode == 0 ? outputDir : null);
    }

    public Task<ArtifactTagResult> TagAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var tags = context.Version is not null ? new[] { context.Version.SemVer } : Array.Empty<string>();
        return Task.FromResult(new ArtifactTagResult(artifact.Name, true, tags));
    }

    public async Task<ArtifactPushResult> PushAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workDir = context.RepositoryRoot;
        var dockerImage = ResolveDockerImage(artifact);
        var outputDir = GetSetting(artifact, "output-directory") ?? workDir;
        var repo = GetSetting(artifact, "repository");

        if (string.IsNullOrWhiteSpace(repo))
        {
            Console.WriteLine("  ! helm push skipped: no repository configured");
            return new ArtifactPushResult(artifact.Name, true, []);
        }

        // Filesystem destination — just copy tgz files
        if (!repo.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !repo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tgz in Directory.EnumerateFiles(outputDir, "*.tgz"))
            {
                var dest = Path.Combine(repo, Path.GetFileName(tgz));
                File.Copy(tgz, dest, overwrite: true);
                Console.WriteLine($"  > copied {Path.GetFileName(tgz)} -> {repo}");
            }

            return new ArtifactPushResult(artifact.Name, true, [repo]);
        }

        var fileEnv = RepositoryEnvironmentFiles.Load(context.RepositoryRoot);
        var auth = ResolveRepoAuth(repo, fileEnv);

        var args = new List<string> { "cm-push", outputDir, repo };
        if (auth.HasCredentials)
        {
            args.Add("--username");
            args.Add(auth.Username ?? string.Empty);
            args.Add("--password");
            args.Add(auth.Secret ?? string.Empty);
        }

        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-push-args")));

        Console.WriteLine($"  > helm {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("helm", args, workDir, artifact, dockerImage, cancellationToken);

        return new ArtifactPushResult(
            artifact.Name,
            result.ExitCode == 0,
            result.ExitCode == 0 ? [$"{repo}/{artifact.Name}"] : []);
    }

    private static string ResolveDockerImage(ArtifactConfig artifact) =>
        Environment.GetEnvironmentVariable("HELM_CONTAINER_IMAGE")
            ?? ToolRunner.GetSetting(artifact, "dockerImage")
            ?? DefaultContainerImage;

    private static string? GetSetting(ArtifactConfig artifact, string key) =>
        ToolRunner.GetSetting(artifact, key);

    /// <summary>
    /// Resolves Chart Museum repository credentials from HELM_REPO_USERNAME /
    /// HELM_REPO_PASSWORD.  Credentials are passed as --username / --password args to
    /// helm cm-push; they are not injected as env vars.
    /// </summary>
    private static FeedAuthResolution ResolveRepoAuth(
        string? configuredRepo,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var username = FeedAuthResolver.GetEnv("HELM_REPO_USERNAME", fileEnv);
        var password = FeedAuthResolver.GetEnv("HELM_REPO_PASSWORD", fileEnv);
        var endpoint = FeedAuthResolver.GetEnv("HELM_REPO_URL", fileEnv) ?? configuredRepo;

        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            return new FeedAuthResolution(false, null, null, endpoint, null, "none");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new FeedAuthResolution(false, null, null, endpoint,
                "HELM_REPO_USERNAME and HELM_REPO_PASSWORD must both be set.", "env");
        }

        return new FeedAuthResolution(true, username, password, endpoint, null, "env");
    }
}
