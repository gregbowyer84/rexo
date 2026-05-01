namespace Rexo.Configuration;

using System.Text.Json;
using NJsonSchema;
using Rexo.Configuration.Models;

public sealed class RepoConfigurationLoader
{
    public const string SupportedSchemaVersion = "1.0";
    // TODO: update SupportedSchemaUri once the remote repository is published
    public const string SupportedSchemaUri = "https://raw.githubusercontent.com/OWNER/repoOS/main/schemas/1.0/schema.json";
    public const string SupportedSchemaPath = "schemas/1.0/schema.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static async Task<RepoConfig> LoadAsync(string configPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Configuration file was not found.", configPath);
        }

        var jsonText = await File.ReadAllTextAsync(configPath, cancellationToken);
        ValidateMetadata(configPath, jsonText);
        await ValidateSchemaAsync(configPath, jsonText, cancellationToken);

        var config = JsonSerializer.Deserialize<RepoConfig>(jsonText, JsonOptions);

        if (config is null)
        {
            throw new InvalidOperationException("Configuration file is empty or invalid.");
        }

        // Resolve extends chain (breadth-first, child overrides base)
        if (config.Extends is { Count: > 0 })
        {
            config = await ResolveExtendsAsync(config, configPath, [], cancellationToken);
        }

        // Apply environment overlay if REXO_OVERLAY is set (overlay wins over everything)
        var overlayEnvPath = Environment.GetEnvironmentVariable("REXO_OVERLAY");
        if (!string.IsNullOrEmpty(overlayEnvPath))
        {
            var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
            var overlayPath = Path.IsPathRooted(overlayEnvPath)
                ? overlayEnvPath
                : Path.GetFullPath(Path.Combine(configDir, overlayEnvPath));

            if (File.Exists(overlayPath))
            {
                var overlayJson = await File.ReadAllTextAsync(overlayPath, cancellationToken);
                var overlayConfig = JsonSerializer.Deserialize<RepoConfig>(overlayJson, JsonOptions);
                if (overlayConfig is not null)
                {
                    config = MergeConfigs(config, overlayConfig);
                }
            }
        }

        return config;
    }

    private static async Task<RepoConfig> ResolveExtendsAsync(
        RepoConfig config,
        string configPath,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
        _ = visited.Add(Path.GetFullPath(configPath));

        // Load and merge all base configs (first listed = lowest priority)
        RepoConfig? merged = null;
        foreach (var extendPath in config.Extends!)
        {
            var basePath = Path.IsPathRooted(extendPath)
                ? extendPath
                : Path.GetFullPath(Path.Combine(configDir, extendPath));

            if (visited.Contains(basePath))
            {
                throw new InvalidOperationException(
                    $"Circular 'extends' reference detected for '{basePath}'.");
            }

            if (!File.Exists(basePath))
            {
                throw new FileNotFoundException(
                    $"Extended config file not found: '{basePath}'.", basePath);
            }

            var baseJsonText = await File.ReadAllTextAsync(basePath, cancellationToken);
            ValidateMetadata(basePath, baseJsonText);
            var baseConfig = JsonSerializer.Deserialize<RepoConfig>(baseJsonText, JsonOptions)
                ?? throw new InvalidOperationException($"Extended config '{basePath}' is empty or invalid.");

            if (baseConfig.Extends is { Count: > 0 })
            {
                baseConfig = await ResolveExtendsAsync(baseConfig, basePath, visited, cancellationToken);
            }

            merged = merged is null ? baseConfig : MergeConfigs(merged, baseConfig);
        }

        // Child config takes priority over merged base
        return merged is null ? config : MergeConfigs(merged, config);
    }

    /// <summary>
    /// Merges <paramref name="child"/> on top of <paramref name="base"/>:
    /// child wins for scalar properties; collections are combined (child appended after base).
    /// </summary>
    private static RepoConfig MergeConfigs(RepoConfig @base, RepoConfig child) =>
        new(
            Name: string.IsNullOrEmpty(child.Name) ? @base.Name : child.Name,
            Commands: MergeDictionaries(@base.Commands, child.Commands),
            Aliases: MergeDictionaries(@base.Aliases, child.Aliases))
        {
            Schema = child.Schema ?? @base.Schema,
            SchemaVersion = child.SchemaVersion ?? @base.SchemaVersion,
            Description = child.Description ?? @base.Description,
            Version = child.Version ?? @base.Version,
            Extends = null, // consumed — do not propagate
            Versioning = child.Versioning ?? @base.Versioning,
            Artifacts = MergeLists(@base.Artifacts, child.Artifacts, child.MergeStrategy),
            Tests = child.Tests ?? @base.Tests,
            Analysis = child.Analysis ?? @base.Analysis,
            PushRulesJson = child.PushRulesJson ?? @base.PushRulesJson,
            MergeStrategy = child.MergeStrategy ?? @base.MergeStrategy,
        };

    private static Dictionary<TKey, TValue> MergeDictionaries<TKey, TValue>(
        Dictionary<TKey, TValue>? @base,
        Dictionary<TKey, TValue>? child)
        where TKey : notnull
    {
        if (@base is null or { Count: 0 }) return child ?? [];
        if (child is null or { Count: 0 }) return @base;

        var result = new Dictionary<TKey, TValue>(@base);
        foreach (var kvp in child)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// Merges two lists according to the specified strategy.
    /// <list type="bullet">
    ///   <item><c>replace</c> — child list discards base; only child entries are kept.</item>
    ///   <item><c>prepend</c> — child entries are inserted before base entries.</item>
    ///   <item>Default (<c>union</c>) — child entries are appended after base entries.</item>
    /// </list>
    /// </summary>
    private static List<T>? MergeLists<T>(List<T>? @base, List<T>? child, string? strategy = null)
    {
        if (@base is null or { Count: 0 }) return child;
        if (child is null or { Count: 0 }) return @base;

        if (string.Equals(strategy, "replace", StringComparison.OrdinalIgnoreCase))
        {
            return child;
        }

        if (string.Equals(strategy, "prepend", StringComparison.OrdinalIgnoreCase))
        {
            var prepended = new List<T>(child);
            prepended.AddRange(@base);
            return prepended;
        }

        // Default: union (append child after base)
        var merged = new List<T>(@base);
        merged.AddRange(child);
        return merged;
    }

    private static void ValidateMetadata(string configPath, string jsonText)
    {
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$schema", out var schemaProp) || schemaProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Configuration must define a '$schema' string property.");
        }

        var schema = schemaProp.GetString();
        // SupportedSchemaUri will be added once the remote repository is published
        var allowedSchemaValues = new[]
        {
            SupportedSchemaPath,
            "./" + SupportedSchemaPath,
        };

        if (string.IsNullOrWhiteSpace(schema) || !allowedSchemaValues.Contains(schema, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported '$schema' value '{schema}'. Expected one of: {string.Join(", ", allowedSchemaValues)}");
        }

        if (!root.TryGetProperty("schemaVersion", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Configuration must define a 'schemaVersion' string property.");
        }

        var version = versionProp.GetString();
        if (!string.Equals(version, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported schemaVersion '{version}'. Supported version is '{SupportedSchemaVersion}'.");
        }
    }

    private static async Task ValidateSchemaAsync(string configPath, string jsonText, CancellationToken cancellationToken)
    {
        var configDirectory = Path.GetDirectoryName(configPath)
            ?? throw new InvalidOperationException("Could not determine config directory.");

        var schemaPath = Path.Combine(configDirectory, "schemas", "1.0", "schema.json");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"Configuration schema file was not found at '{schemaPath}'.", schemaPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var schema = await JsonSchema.FromFileAsync(schemaPath, CancellationToken.None);

        cancellationToken.ThrowIfCancellationRequested();
        var errors = schema.Validate(jsonText);
        if (errors.Count > 0)
        {
            var details = string.Join(
                Environment.NewLine,
                errors.Select(e => $"- {e.Path}: {e.Kind} ({e.Property})"));

            throw new InvalidOperationException(
                "Configuration does not match the declared schema. Validation errors:" +
                Environment.NewLine +
                details);
        }
    }
}
