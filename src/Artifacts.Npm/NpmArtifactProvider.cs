namespace Rexo.Artifacts.Npm;

using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;

/// <summary>npm pack / publish artifact provider.  Type key: <c>npm</c>.</summary>
public sealed class NpmArtifactProvider : IArtifactProvider
{
    public static void Register(ArtifactProviderRegistry registry) =>
        registry.Register("npm", new NpmArtifactProvider());

    private const string DefaultContainerImage = "node:lts-alpine";

    public string Type => "npm";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var workDir = GetSetting(artifact, "directory") ?? context.RepositoryRoot;
        var dockerImage = ResolveDockerImage(artifact, "NPM_CONTAINER_IMAGE");

        var args = new List<string> { "pack" };
        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-build-args")));

        Console.WriteLine($"  > npm {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("npm", args, workDir, artifact, dockerImage, cancellationToken);

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
        var workDir = GetSetting(artifact, "directory") ?? context.RepositoryRoot;
        var registry = GetSetting(artifact, "registry");
        var access = GetSetting(artifact, "access") ?? "public";
        var tag = GetSetting(artifact, "tag") ?? context.Version?.SemVer;
        var dockerImage = ResolveDockerImage(artifact, "NPM_CONTAINER_IMAGE");

        var fileEnv = RepositoryEnvironmentFiles.Load(context.RepositoryRoot);
        var auth = ResolveAuth(registry, GetSetting(artifact, "tokenEnv"), fileEnv);
        IReadOnlyDictionary<string, string?>? envOverrides = auth.HasCredentials
            ? new Dictionary<string, string?> { ["NPM_TOKEN"] = auth.Secret }
            : null;

        var args = new List<string> { "publish", "--access", access };
        if (!string.IsNullOrWhiteSpace(registry))
        {
            args.Add("--registry");
            args.Add(registry);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            args.Add("--tag");
            args.Add(tag);
        }

        args.AddRange(ToolRunner.ParseExtraArgs(GetSetting(artifact, "extra-push-args")));

        Console.WriteLine($"  > npm {ToolRunner.FormatArgs(args)}");
        var result = await ToolRunner.RunAsync("npm", args, workDir, artifact, dockerImage, cancellationToken, envOverrides: envOverrides);

        return new ArtifactPushResult(
            artifact.Name,
            result.ExitCode == 0,
            result.ExitCode == 0 ? [$"{artifact.Name}@{tag ?? "latest"}"] : []);
    }

    private static string ResolveDockerImage(ArtifactConfig artifact, string envVar) =>
        Environment.GetEnvironmentVariable(envVar)
            ?? ToolRunner.GetSetting(artifact, "dockerImage")
            ?? DefaultContainerImage;

    private static string? GetSetting(ArtifactConfig artifact, string key) =>
        ToolRunner.GetSetting(artifact, key);

    /// <summary>
    /// Resolves npm publish credentials.  Order: configured tokenEnv → NPM_TOKEN →
    /// NODE_AUTH_TOKEN → GITHUB_TOKEN for npm.pkg.github.com.
    /// The resolved token is injected as <c>NPM_TOKEN</c> for the publish invocation.
    /// </summary>
    private static FeedAuthResolution ResolveAuth(
        string? registry,
        string? tokenEnvOverride,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var envName = string.IsNullOrWhiteSpace(tokenEnvOverride) ? "NPM_TOKEN" : tokenEnvOverride;
        var token = FeedAuthResolver.GetEnv(envName, fileEnv)
                    ?? FeedAuthResolver.GetEnv("NODE_AUTH_TOKEN", fileEnv);

        if (string.IsNullOrWhiteSpace(token))
        {
            // CI-native fallback for GitHub Packages.
            if (!string.IsNullOrWhiteSpace(registry) &&
                registry.Contains("npm.pkg.github.com", StringComparison.OrdinalIgnoreCase))
            {
                token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    return new FeedAuthResolution(true, null, token, registry, null, "github-token");
                }
            }

            return new FeedAuthResolution(false, null, null, registry, null, "none");
        }

        return new FeedAuthResolution(true, null, token, registry, null, "env");
    }
}
