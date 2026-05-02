namespace Rexo.Cli;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rexo.Configuration;
using Rexo.Configuration.Models;

internal static class PolicySourceLoader
{
    private const string PolicySourcesEnv = "REXO_POLICY_SOURCES";
    private const string PolicyTrustEnv = "REXO_POLICY_TRUST";
    private const string RequirePinnedEnv = "REXO_POLICY_REQUIRE_PINNED";
    private const string NuGetPolicySourceEnv = "REXO_NUGET_POLICY_SOURCE";
    private static readonly HttpClient HttpClient = new();

    public static async Task<PolicyConfig> LoadPoliciesFromEnvironmentAsync(
        string workingDir,
        bool debug,
        CancellationToken cancellationToken)
    {
        var sourcesValue = Environment.GetEnvironmentVariable(PolicySourcesEnv);
        if (string.IsNullOrWhiteSpace(sourcesValue))
        {
            return new PolicyConfig();
        }

        var merged = new PolicyConfig();
        var sources = sourcesValue
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var source in sources)
        {
            try
            {
                var content = await LoadPolicyContentAsync(source, workingDir, cancellationToken);
                var extension = source.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                source.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                    ? ".yaml"
                    : ".json";
                var tempPolicy = Path.Combine(Path.GetTempPath(), $"rexo-policy-{ComputeSha256Hex(source)}{extension}");
                await File.WriteAllTextAsync(tempPolicy, content, cancellationToken);

                var config = await RepoConfigurationLoader.LoadPolicyAsync(tempPolicy, cancellationToken);
                if (config is not null)
                {
                    merged = ConfigBuilder.MergePolicies(merged, config);
                }

                if (debug)
                {
                    Console.WriteLine($"[debug] Loaded remote policy source: {source}");
                }
            }
            catch (Exception ex)
            {
                if (debug)
                {
                    Console.WriteLine($"[debug] Remote policy source skipped ({source}): {ex.Message}");
                }
            }
        }

        return merged;
    }

    private static async Task<string> LoadPolicyContentAsync(string source, string workingDir, CancellationToken cancellationToken)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadHttpPolicyAsync(source, workingDir, cancellationToken);
        }

        if (source.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadGitPolicyAsync(source, workingDir, cancellationToken);
        }

        if (source.StartsWith("nuget:", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadNuGetPolicyAsync(source, workingDir, cancellationToken);
        }

        // fallback: local file source listed in env
        var fullPath = Path.IsPathRooted(source)
            ? source
            : Path.GetFullPath(Path.Combine(workingDir, source));
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private static async Task<string> LoadHttpPolicyAsync(string reference, string workingDir, CancellationToken cancellationToken)
    {
        var uri = new Uri(reference);
        EnsureTrusted(uri.Host);

        var requirePinned = IsPinnedRequired();
        var expectedSha = ParseShaFromFragment(uri.Fragment);
        if (requirePinned && string.IsNullOrWhiteSpace(expectedSha))
        {
            throw new InvalidOperationException("Pinned policy required. Append #sha256=<hex> to the HTTP policy URL.");
        }

        var cachePath = GetCachePath(workingDir, reference);

        try
        {
            var content = await HttpClient.GetStringAsync(uri, cancellationToken);
            ValidateShaIfPresent(content, expectedSha);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, content, cancellationToken);
            return content;
        }
        catch
        {
            if (File.Exists(cachePath))
            {
                return await File.ReadAllTextAsync(cachePath, cancellationToken);
            }

            throw;
        }
    }

    private static async Task<string> LoadGitPolicyAsync(string reference, string workingDir, CancellationToken cancellationToken)
    {
        // git+<repo>@<ref>#<path>
        var value = reference[4..];
        var hashIndex = value.LastIndexOf('#');
        var atIndex = value.LastIndexOf('@');
        if (hashIndex <= 0 || atIndex <= 0 || atIndex > hashIndex)
        {
            throw new InvalidOperationException("Invalid git policy reference. Use git+<repo>@<ref>#<path>.");
        }

        var repo = value[..atIndex];
        var gitRef = value[(atIndex + 1)..hashIndex];
        var path = value[(hashIndex + 1)..];

        if (IsPinnedRequired() && (string.IsNullOrWhiteSpace(gitRef) ||
            gitRef.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            gitRef.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            gitRef.Equals("latest", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Pinned git policy required. Use a commit SHA or immutable tag in @<ref>.");
        }

        if (Uri.TryCreate(repo, UriKind.Absolute, out var repoUri))
        {
            EnsureTrusted(repoUri.Host);
        }

        var cachePath = GetCachePath(workingDir, reference);

        try
        {
            var output = await RunProcessAsync(
                "git",
                ["-C", repo, "show", $"{gitRef}:{path}"],
                workingDir,
                cancellationToken);
            if (output.ExitCode != 0)
            {
                throw new InvalidOperationException($"git show failed: {output.Output}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, output.Output, cancellationToken);
            return output.Output;
        }
        catch
        {
            if (File.Exists(cachePath))
            {
                return await File.ReadAllTextAsync(cachePath, cancellationToken);
            }

            throw;
        }
    }

    private static async Task<string> LoadNuGetPolicyAsync(string reference, string workingDir, CancellationToken cancellationToken)
    {
        // nuget:<packageId>@<version>#<path>
        var value = reference[6..];
        var hashIndex = value.LastIndexOf('#');
        var atIndex = value.LastIndexOf('@');
        if (hashIndex <= 0 || atIndex <= 0 || atIndex > hashIndex)
        {
            throw new InvalidOperationException("Invalid nuget policy reference. Use nuget:<packageId>@<version>#<path>.");
        }

        var packageId = value[..atIndex];
        var version = value[(atIndex + 1)..hashIndex];
        var pathInPackage = value[(hashIndex + 1)..];

        if (IsPinnedRequired() && string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("Pinned NuGet policy required. Specify @<version>.");
        }

        var source = Environment.GetEnvironmentVariable(NuGetPolicySourceEnv) ?? "https://api.nuget.org";
        if (Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
        {
            EnsureTrusted(sourceUri.Host);
        }

        var packageUrl = BuildNuGetPackageUrl(source, packageId, version);
        var cachePath = GetCachePath(workingDir, reference);

        try
        {
            using var stream = await HttpClient.GetStreamAsync(packageUrl, cancellationToken);
            var tempNupkg = Path.Combine(Path.GetTempPath(), $"rexo-policy-{ComputeSha256Hex(reference)}.nupkg");
            await using (var file = File.Create(tempNupkg))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }

            using var archive = ZipFile.OpenRead(tempNupkg);
            var entry = archive.GetEntry(pathInPackage.Replace('\\', '/'));
            if (entry is null)
            {
                throw new InvalidOperationException($"Policy path '{pathInPackage}' not found in package '{packageId}@{version}'.");
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var content = await reader.ReadToEndAsync(cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, content, cancellationToken);
            return content;
        }
        catch
        {
            if (File.Exists(cachePath))
            {
                return await File.ReadAllTextAsync(cachePath, cancellationToken);
            }

            throw;
        }
    }

    private static string BuildNuGetPackageUrl(string source, string packageId, string version)
    {
        var root = source.TrimEnd('/');
        if (!root.EndsWith("/v3-flatcontainer", StringComparison.OrdinalIgnoreCase))
        {
            root += "/v3-flatcontainer";
        }

        var id = packageId.ToLowerInvariant();
        var v = version.ToLowerInvariant();
        return $"{root}/{id}/{v}/{id}.{v}.nupkg";
    }

    private static string GetCachePath(string workingDir, string reference)
    {
        var key = ComputeSha256Hex(reference);
        return Path.Combine(workingDir, ".rexo", "cache", "policies", $"{key}.policy.json");
    }

    private static void EnsureTrusted(string host)
    {
        var trust = Environment.GetEnvironmentVariable(PolicyTrustEnv);
        if (string.IsNullOrWhiteSpace(trust))
        {
            // Safe defaults for policy distribution.
            trust = "raw.githubusercontent.com;github.com;api.nuget.org";
        }

        if (trust.Equals("allow-all", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allowed = trust
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowed.Any(a => host.Equals(a, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + a, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Untrusted policy source host '{host}'. Set {PolicyTrustEnv} to allow it.");
        }
    }

    private static bool IsPinnedRequired() =>
        string.Equals(Environment.GetEnvironmentVariable(RequirePinnedEnv), "true", StringComparison.OrdinalIgnoreCase);

    private static string? ParseShaFromFragment(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var value = fragment.TrimStart('#');
        const string prefix = "sha256=";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : null;
    }

    private static void ValidateShaIfPresent(string content, string? expectedSha)
    {
        if (string.IsNullOrWhiteSpace(expectedSha))
        {
            return;
        }

        var actual = ComputeSha256Hex(content);
        if (!actual.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTP policy SHA-256 mismatch.");
        }
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
        {
            return (-1, string.Empty);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, stdout + stderr);
    }
}
