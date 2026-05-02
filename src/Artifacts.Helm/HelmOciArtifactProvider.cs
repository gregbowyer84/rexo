namespace Rexo.Artifacts.Helm;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Environment;
using Rexo.Core.Models;

public sealed class HelmOciArtifactProvider : IArtifactProvider
{
    private const string DefaultHelmContainerImage = "alpine/helm:3.17.3";
    private readonly Func<ArtifactConfig, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string?>?, string?, CancellationToken, Task<(int ExitCode, string Output)>> _runHelmAsync;

    public HelmOciArtifactProvider(
        Func<ArtifactConfig, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string?>?, string?, CancellationToken, Task<(int ExitCode, string Output)>>? runHelmAsync = null)
    {
        _runHelmAsync = runHelmAsync ?? RunHelmAsync;
    }

    public string Type => "helm-oci";

    public IReadOnlyList<string> GetPlannedTags(ArtifactConfig artifact, ExecutionContext context) =>
        context.Version is not null
            ? [context.Version.SemVer]
            : Array.Empty<string>();

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var chartPath = GetSetting(artifact, "chartPath") ?? "chart";
        var output = GetSetting(artifact, "output") ?? Path.Combine("artifacts", "charts");
        var chartName = GetSetting(artifact, "chart") ?? artifact.Name;
        var version = context.Version?.SemVer;

        Directory.CreateDirectory(Path.Combine(context.RepositoryRoot, output));

        var args = new List<string>
        {
            "package",
            chartPath,
            "--destination",
            output,
        };

        if (!string.IsNullOrWhiteSpace(version))
        {
            args.Add("--version");
            args.Add(version);
            args.Add("--app-version");
            args.Add(version);
        }

        Console.WriteLine($"  > helm {FormatArguments(args)}");
        var result = await _runHelmAsync(artifact, args, context.RepositoryRoot, null, null, cancellationToken);
        if (result.ExitCode != 0)
        {
            return new ArtifactBuildResult(artifact.Name, false, null);
        }

        var packagePath = TryFindPackagePath(context.RepositoryRoot, output, chartName, version);
        if (string.IsNullOrWhiteSpace(packagePath) && !string.IsNullOrWhiteSpace(version))
        {
            packagePath = Path.Combine(context.RepositoryRoot, output, $"{chartName}-{version}.tgz");
        }

        return new ArtifactBuildResult(artifact.Name, true, packagePath);
    }

    public Task<ArtifactTagResult> TagAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
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
        var chartName = GetSetting(artifact, "chart") ?? artifact.Name;
        var output = GetSetting(artifact, "output") ?? Path.Combine("artifacts", "charts");
        var version = context.Version?.SemVer;

        await TryHelmRegistryLoginAsync(artifact, context, cancellationToken);

        var packagePath = TryFindPackagePath(context.RepositoryRoot, output, chartName, version);
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            var buildResult = await BuildAsync(artifact, context, cancellationToken);
            if (!buildResult.Success || string.IsNullOrWhiteSpace(buildResult.Location))
            {
                return new ArtifactPushResult(artifact.Name, false, Array.Empty<string>());
            }

            packagePath = buildResult.Location;
        }

        var destination = ResolveOciDestination(artifact);
        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("Helm OCI destination is required. Set settings.oci or settings.registry + settings.repository.");
            return new ArtifactPushResult(artifact.Name, false, Array.Empty<string>());
        }

        var pushArgs = new[] { "push", packagePath, destination };
        Console.WriteLine($"  > helm {FormatArguments(pushArgs)}");
        var pushResult = await _runHelmAsync(artifact, pushArgs, context.RepositoryRoot, null, null, cancellationToken);

        if (pushResult.ExitCode != 0)
        {
            return new ArtifactPushResult(artifact.Name, false, Array.Empty<string>());
        }

        var reference = !string.IsNullOrWhiteSpace(version)
            ? $"{destination}/{chartName}:{version}"
            : $"{destination}/{chartName}";

        return new ArtifactPushResult(artifact.Name, true, [reference]);
    }

    private async Task TryHelmRegistryLoginAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var fileEnv = RepositoryEnvironmentFiles.Load(context.RepositoryRoot);
        var auth = FeedAuthResolver.ResolveHelm(
            configuredRegistry: GetSetting(artifact, "loginRegistry") ?? GetSetting(artifact, "registry"),
            fileEnv: fileEnv);

        if (!string.IsNullOrWhiteSpace(auth.Error))
        {
            Console.Error.WriteLine(auth.Error);
            return;
        }

        if (!auth.HasCredentials)
        {
            return;
        }

        var registry = auth.Endpoint;
        if (string.IsNullOrWhiteSpace(registry))
        {
            Console.Error.WriteLine("Helm login registry could not be determined. Set settings.registry or HELM_REGISTRY.");
            return;
        }

        var args = new[] { "registry", "login", registry, "--username", auth.Username!, "--password-stdin" };
        Console.WriteLine($"  > helm {FormatArguments(args)}");
        await _runHelmAsync(artifact, args, context.RepositoryRoot, null, auth.Secret! + Environment.NewLine, cancellationToken);
    }

    private static string? ResolveOciDestination(ArtifactConfig artifact)
    {
        var oci = GetSetting(artifact, "oci");
        if (!string.IsNullOrWhiteSpace(oci))
        {
            return oci.StartsWith("oci://", StringComparison.OrdinalIgnoreCase)
                ? oci
                : "oci://" + oci;
        }

        var registry = GetSetting(artifact, "registry") ?? Environment.GetEnvironmentVariable("HELM_REGISTRY");
        var repository = GetSetting(artifact, "repository") ?? Environment.GetEnvironmentVariable("HELM_REPOSITORY");
        if (string.IsNullOrWhiteSpace(registry) || string.IsNullOrWhiteSpace(repository))
        {
            return null;
        }

        return $"oci://{registry.TrimEnd('/')}/{repository.Trim('/')}";
    }

    private static string? TryFindPackagePath(string repoRoot, string output, string chartName, string? version)
    {
        var outputPath = Path.Combine(repoRoot, output);
        if (!Directory.Exists(outputPath))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            var expected = Path.Combine(outputPath, $"{chartName}-{version}.tgz");
            if (File.Exists(expected))
            {
                return expected;
            }
        }

        return Directory
            .EnumerateFiles(outputPath, $"{chartName}-*.tgz", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
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

    private static string FormatArguments(IReadOnlyList<string> args) =>
        string.Join(" ", args.Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static async Task<(int ExitCode, string Output)> RunHelmAsync(
        ArtifactConfig artifact,
        IReadOnlyList<string> args,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? envOverrides,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var native = await TryRunHostHelmAsync(args, workingDirectory, envOverrides, standardInput, cancellationToken);
        if (native is not null)
        {
            return native.Value;
        }

        if (string.Equals(GetSetting(artifact, "useDocker"), "false", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "helm CLI is not installed and Docker fallback is disabled (settings.useDocker = false).");
        }

        var dockerImage = Environment.GetEnvironmentVariable("HELM_CONTAINER_IMAGE")
            ?? GetSetting(artifact, "dockerImage")
            ?? DefaultHelmContainerImage;
        Console.WriteLine($"  > helm not found on host, falling back to dockerized Helm runtime ({dockerImage})");
        return await RunHelmViaDockerAsync(args, workingDirectory, envOverrides, standardInput, dockerImage, cancellationToken);
    }

    private static async Task<(int ExitCode, string Output)?> TryRunHostHelmAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? envOverrides,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunProcessAsync(
                fileName: "helm",
                args: args,
                workingDirectory: workingDirectory,
                envOverrides: envOverrides,
                standardInput: standardInput,
                cancellationToken: cancellationToken);
        }
        catch (Win32Exception ex) when (IsCommandNotFound(ex))
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static async Task<(int ExitCode, string Output)> RunHelmViaDockerAsync(
        IReadOnlyList<string> helmArgs,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? envOverrides,
        string? standardInput,
        string helmContainerImage,
        CancellationToken cancellationToken)
    {
        var dockerArgs = new List<string>
        {
            "run",
            "--rm",
            "-v",
            $"{workingDirectory}:/work",
            "-w",
            "/work",
        };

        if (standardInput is not null)
        {
            dockerArgs.Add("-i");
        }

        if (envOverrides is not null)
        {
            foreach (var (key, value) in envOverrides)
            {
                if (value is null)
                {
                    continue;
                }

                dockerArgs.Add("-e");
                dockerArgs.Add($"{key}={value}");
            }
        }

        dockerArgs.Add(helmContainerImage);
        dockerArgs.AddRange(helmArgs);

        return await RunProcessAsync(
            fileName: "docker",
            args: dockerArgs,
            workingDirectory: workingDirectory,
            envOverrides: null,
            standardInput: standardInput,
            cancellationToken: cancellationToken);
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? envOverrides,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = standardInput is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (envOverrides is not null)
        {
            foreach (var (key, value) in envOverrides)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        process.Start();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);

        return (process.ExitCode, stdout + stderr);
    }

    private static bool IsCommandNotFound(Win32Exception exception) =>
        exception.NativeErrorCode is 2 or 3;
}
