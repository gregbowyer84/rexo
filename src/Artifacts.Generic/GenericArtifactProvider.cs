namespace Rexo.Artifacts.Generic;

using System.IO.Compression;
using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

/// <summary>Preview: Generic file artifact provider — zip / tar.gz packaging using built-in .NET compression.</summary>
public sealed class GenericArtifactProvider : IArtifactProvider
{
    public static void Register(ArtifactProviderRegistry registry) =>
        registry.Register("generic", new GenericArtifactProvider());

    public string Type => "generic";

    public async Task<ArtifactBuildResult> BuildAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var source = GetSetting(artifact, "source") ?? context.RepositoryRoot;
        var format = GetSetting(artifact, "format") ?? "zip";
        var outputDir = GetSetting(artifact, "output") ?? "artifacts/generic";

        var absoluteSource = System.IO.Path.IsPathRooted(source)
            ? source
            : System.IO.Path.Combine(context.RepositoryRoot, source);

        var absoluteOutput = System.IO.Path.IsPathRooted(outputDir)
            ? outputDir
            : System.IO.Path.Combine(context.RepositoryRoot, outputDir);

        Directory.CreateDirectory(absoluteOutput);

        var version = context.Version?.SemVer ?? "0.0.0";
        var archiveName = $"{artifact.Name}-{version}.{format}";
        var archivePath = System.IO.Path.Combine(absoluteOutput, archiveName);

        Console.WriteLine($"  > Creating {format} archive: {archiveName}");

        try
        {
            if (format.Equals("zip", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                ZipFile.CreateFromDirectory(absoluteSource, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            else
            {
                Console.Error.WriteLine($"  Unsupported format '{format}'. Only 'zip' is supported.");
                return new ArtifactBuildResult(artifact.Name, false, null);
            }

            await Task.CompletedTask;
            return new ArtifactBuildResult(artifact.Name, true, archivePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Error creating archive: {ex.Message}");
            return new ArtifactBuildResult(artifact.Name, false, null);
        }
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

    public Task<ArtifactPushResult> PushAsync(
        ArtifactConfig artifact,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var destination = GetSetting(artifact, "destination");

        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("  No destination configured for generic artifact push.");
            return Task.FromResult(new ArtifactPushResult(artifact.Name, false, []));
        }

        var outputDir = GetSetting(artifact, "output") ?? "artifacts/generic";
        var absoluteOutput = System.IO.Path.IsPathRooted(outputDir)
            ? outputDir
            : System.IO.Path.Combine(context.RepositoryRoot, outputDir);

        var absoluteDestination = System.IO.Path.IsPathRooted(destination)
            ? destination
            : System.IO.Path.Combine(context.RepositoryRoot, destination);

        try
        {
            Directory.CreateDirectory(absoluteDestination);

            var files = Directory.GetFiles(absoluteOutput, $"{artifact.Name}-*");
            foreach (var file in files)
            {
                var destFile = System.IO.Path.Combine(absoluteDestination, System.IO.Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
                Console.WriteLine($"  Copied: {destFile}");
            }

            return Task.FromResult(new ArtifactPushResult(artifact.Name, true, [absoluteDestination]));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Error copying artifact: {ex.Message}");
            return Task.FromResult(new ArtifactPushResult(artifact.Name, false, []));
        }
    }

    private static string? GetSetting(ArtifactConfig artifact, string key) =>
        artifact.Settings.TryGetValue(key, out var val) ? val.GetString() : null;
}
