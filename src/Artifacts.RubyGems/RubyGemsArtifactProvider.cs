namespace Rexo.Artifacts.RubyGems;

using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;

/// <summary>
/// RubyGems artifact provider — <c>gem build</c> for build, <c>gem push</c> for push.
/// Type key: <c>rubygems</c>.
/// </summary>
public sealed class RubyGemsArtifactProvider : IArtifactProvider
{
    public static void Register(ArtifactProviderRegistry registry) =>
        registry.Register("rubygems", new RubyGemsArtifactProvider());

    private const string DefaultContainerImage = "ruby:3-alpine";

    public string Type => "rubygems";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workDir = GetWorkDir(artifact, context);
        var dockerImage = ResolveDockerImage(artifact);
        var gemspec = GetSetting(artifact, "gemspec") ?? "*.gemspec";

        var args = new List<string> { "build", gemspec };
        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-build-args")));

        Console.WriteLine($"  > gem {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("gem", args, workDir, artifact, dockerImage, cancellationToken);

        return new ArtifactBuildResult(artifact.Name, result.ExitCode == 0, result.ExitCode == 0 ? workDir : null);
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
        var workDir = GetWorkDir(artifact, context);
        var dockerImage = ResolveDockerImage(artifact);
        var source = GetSetting(artifact, "source");
        var gemPattern = GetSetting(artifact, "gem-pattern") ?? "*.gem";

        var fileEnv = RepositoryEnvironmentFiles.Load(context.RepositoryRoot);
        var apiKeyEnv = GetSetting(artifact, "apiKeyEnv");
        var auth = ResolveAuth(apiKeyEnv, fileEnv);
        IReadOnlyDictionary<string, string?>? envOverrides = auth.HasCredentials
            ? new Dictionary<string, string?> { ["GEM_HOST_API_KEY"] = auth.Secret }
            : null;

        var args = new List<string> { "push", gemPattern };
        if (!string.IsNullOrWhiteSpace(source))
        {
            args.Add("--source");
            args.Add(source);
        }

        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-push-args")));

        Console.WriteLine($"  > gem {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("gem", args, workDir, artifact, dockerImage, cancellationToken, envOverrides: envOverrides);

        var tag = context.Version?.SemVer ?? "latest";
        return new ArtifactPushResult(
            artifact.Name,
            result.ExitCode == 0,
            result.ExitCode == 0 ? [$"{artifact.Name}-{tag}.gem"] : []);
    }

    private static string GetWorkDir(ArtifactConfig artifact, ExecutionContext context)
    {
        var dir = GetSetting(artifact, "directory");
        if (string.IsNullOrWhiteSpace(dir))
        {
            return context.RepositoryRoot;
        }

        return Path.IsPathRooted(dir) ? dir : Path.Combine(context.RepositoryRoot, dir);
    }

    private static string ResolveDockerImage(ArtifactConfig artifact) =>
        Environment.GetEnvironmentVariable("GEM_CONTAINER_IMAGE")
            ?? ToolRunner.GetSetting(artifact, "dockerImage")
            ?? DefaultContainerImage;

    private static string? GetSetting(ArtifactConfig artifact, string key) =>
        ToolRunner.GetSetting(artifact, key);

    /// <summary>
    /// Resolves RubyGems push credentials.  Order: configured apiKeyEnv var →
    /// GEM_HOST_API_KEY → RUBYGEMS_API_KEY.
    /// The resolved key is injected as GEM_HOST_API_KEY for the gem push invocation.
    /// </summary>
    private static FeedAuthResolution ResolveAuth(
        string? configuredApiKeyEnv,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var envName = string.IsNullOrWhiteSpace(configuredApiKeyEnv) ? "GEM_HOST_API_KEY" : configuredApiKeyEnv;
        var apiKey = FeedAuthResolver.GetEnv(envName, fileEnv)
                     ?? FeedAuthResolver.GetEnv("RUBYGEMS_API_KEY", fileEnv);

        return string.IsNullOrWhiteSpace(apiKey)
            ? new FeedAuthResolution(false, null, null, null, null, "none")
            : new FeedAuthResolution(true, null, apiKey, null, null, "env");
    }
}
