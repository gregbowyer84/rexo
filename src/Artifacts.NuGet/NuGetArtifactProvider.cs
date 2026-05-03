namespace Rexo.Artifacts.NuGet;

using System.Diagnostics;
using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;

public sealed class NuGetArtifactProvider : IArtifactProvider
{
    public static void Register(ArtifactProviderRegistry registry) =>
        registry.Register("nuget", new NuGetArtifactProvider());

    public string Type => "nuget";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var project = GetSetting(artifact, "project") ?? string.Empty;
        var output = GetSetting(artifact, "output") ?? "artifacts/packages";
        var version = context.Version?.SemVer;

        Directory.CreateDirectory(Path.Combine(context.RepositoryRoot, output));

        var args = $"pack {project} --configuration Release --output {output} --no-build";
        if (!string.IsNullOrEmpty(version))
        {
            args += $" /p:Version={version}";
        }

        Console.WriteLine($"  > dotnet {args}");

        var result = await RunDotnetAsync(args, context.RepositoryRoot, cancellationToken);

        return new ArtifactBuildResult(
            Name: artifact.Name,
            Success: result.ExitCode == 0,
            Location: result.ExitCode == 0 ? Path.Combine(context.RepositoryRoot, output) : null);
    }

    public Task<ArtifactTagResult> TagAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        // NuGet packages are versioned at pack time, no separate tagging step needed
        var tags = context.Version is not null
            ? new[] { context.Version.SemVer }
            : Array.Empty<string>();

        return Task.FromResult(new ArtifactTagResult(artifact.Name, true, tags));
    }

    public async Task<ArtifactPushResult> PushAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var source = GetSetting(artifact, "source") ?? "https://api.nuget.org/v3/index.json";
        var output = GetSetting(artifact, "output") ?? "artifacts/packages";
        var apiKeyEnvVar = GetSetting(artifact, "apiKeyEnv") ?? "NUGET_API_KEY";
        var fileEnv = RepositoryEnvironmentFiles.Load(context.RepositoryRoot);
        var auth = ResolveAuth(source, apiKeyEnvVar, fileEnv);
        if (!auth.HasCredentials)
        {
            Console.Error.WriteLine("NuGet auth preflight failed: no API token resolved from env/CI identity.");
            return new ArtifactPushResult(artifact.Name, false, Array.Empty<string>());
        }

        var args = $"nuget push {output}/*.nupkg --source {source} --skip-duplicate";
        if (!string.IsNullOrEmpty(auth.Secret))
        {
            args += $" --api-key {auth.Secret}";
        }

        Console.WriteLine($"  > dotnet {args}");

        var result = await RunDotnetAsync(args, context.RepositoryRoot, cancellationToken);
        var published = result.ExitCode == 0
            ? new[] { $"{source}/{artifact.Name}" }
            : Array.Empty<string>();

        return new ArtifactPushResult(artifact.Name, result.ExitCode == 0, published);
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", arguments)
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
        artifact.Settings.TryGetValue(key, out var value) ? GetString(value) : null;

    private static string? GetString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Number => value.GetRawText(),
            _ => value.ToString(),
        };

    /// <summary>
    /// Resolves NuGet push credentials.  Order: configured apiKeyEnv / NUGET_API_KEY /
    /// NUGET_AUTH_TOKEN → GITHUB_TOKEN for nuget.pkg.github.com → SYSTEM_ACCESSTOKEN
    /// for Azure Artifacts.
    /// </summary>
    private static FeedAuthResolution ResolveAuth(
        string source,
        string? configuredApiKeyEnv,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var envName = string.IsNullOrWhiteSpace(configuredApiKeyEnv) ? "NUGET_API_KEY" : configuredApiKeyEnv;
        var secret = FeedAuthResolver.GetEnv(envName, fileEnv)
                     ?? FeedAuthResolver.GetEnv("NUGET_AUTH_TOKEN", fileEnv);

        if (string.IsNullOrWhiteSpace(secret))
        {
            if (source.Contains("nuget.pkg.github.com", StringComparison.OrdinalIgnoreCase))
            {
                secret = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            }
            else
            {
                secret = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
            }

            if (!string.IsNullOrWhiteSpace(secret))
            {
                return new FeedAuthResolution(true, null, secret, source, null, "ci-token");
            }

            return new FeedAuthResolution(false, null, null, source, null, "none");
        }

        return new FeedAuthResolution(true, null, secret, source, null, "env");
    }
}
