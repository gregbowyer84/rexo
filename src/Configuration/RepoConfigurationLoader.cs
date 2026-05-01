namespace Rexo.Configuration;

using System.Collections;
using System.Text.Json;
using NJsonSchema;
using Rexo.Configuration.Models;
using YamlDotNet.Serialization;

public sealed class RepoConfigurationLoader
{
    public const string SupportedSchemaVersion = "1.0";
    public const string SupportedSchemaUri = "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/schema.json";
    public const string SupportedSchemaPath = "schema.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly IDeserializer YamlDeserializer =
        new DeserializerBuilder().Build();

    public static async Task<RepoConfig> LoadAsync(string configPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Configuration file was not found.", configPath);
        }

        var jsonText = await ReadAsJsonAsync(configPath, cancellationToken);
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
                var overlayJson = await ReadAsJsonAsync(overlayPath, cancellationToken);
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

            var baseJsonText = await ReadAsJsonAsync(basePath, cancellationToken);
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

    public static async Task<PolicyConfig?> LoadPolicyAsync(string policyPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(policyPath))
        {
            throw new FileNotFoundException("Policy file was not found.", policyPath);
        }

        var policyJson = await ReadAsJsonAsync(policyPath, cancellationToken);
        return JsonSerializer.Deserialize<PolicyConfig>(policyJson, JsonOptions);
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
        var allowedSchemaValues = new[]
        {
            SupportedSchemaUri,
            SupportedSchemaPath,
            "./" + SupportedSchemaPath,
            "../" + SupportedSchemaPath,
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

        cancellationToken.ThrowIfCancellationRequested();

        JsonSchema schema;
        // Look for schema.json next to the config file, then one level up (handles .rexo/ layout).
        var schemaPath = Path.Combine(configDirectory, "schema.json");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(configDirectory, "..", "schema.json");
        if (File.Exists(schemaPath))
        {
            schema = await JsonSchema.FromFileAsync(schemaPath, CancellationToken.None);
        }
        else
        {
            // Fall back to the schema embedded in this assembly so the tool works
            // without a local schemas/ directory (e.g. when installed via dotnet tool install).
            var asm = typeof(RepoConfigurationLoader).Assembly;
            const string resourceName = "Rexo.Configuration.schema.1.0.json";
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded schema resource '{resourceName}' was not found in the assembly.");
            using var reader = new StreamReader(stream);
            var schemaJson = await reader.ReadToEndAsync(cancellationToken);
            schema = await JsonSchema.FromJsonAsync(schemaJson, CancellationToken.None);
        }

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

    private static async Task<string> ReadAsJsonAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        if (!IsYamlPath(path))
        {
            return text;
        }

        using var reader = new StringReader(text);
        var yamlObject = YamlDeserializer.Deserialize(reader);
        var normalized = NormalizeYamlObject(yamlObject);
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static bool IsYamlPath(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    private static object? NormalizeYamlObject(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or
                float or double or decimal:
                return value;
            case IDictionary dictionary:
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    result[key] = NormalizeYamlObject(entry.Value);
                }

                return result;
            }
            case IEnumerable enumerable when value is not string:
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizeYamlObject(item));
                }

                return list;
            }
            default:
                return value.ToString();
        }
    }
}
