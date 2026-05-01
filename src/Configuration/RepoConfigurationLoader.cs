namespace Rexo.Configuration;

using System.Collections;
using System.Text.Json;
using NJsonSchema;
using Rexo.Configuration.Models;
using YamlDotNet.Serialization;

public sealed class RepoConfigurationLoader
{
    public const string SupportedSchemaVersion = "1.0";
    public const string SupportedRexoSchemaUri = "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json";
    public const string SupportedRexoSchemaPath = "rexo.schema.json";
    public const string SupportedRexoSchemaPathInRexoFolder = ".rexo/rexo.schema.json";
    public const string SupportedPolicySchemaUri = "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/policy.schema.json";
    public const string SupportedPolicySchemaPath = "policy.schema.json";
    public const string SupportedPolicySchemaPathInRexoFolder = ".rexo/policy.schema.json";

    // Legacy values kept for backward compatibility.
    public const string LegacySchemaUri = "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/schema.json";
    public const string LegacySchemaPath = "schema.json";
    public const string LegacySchemaPathInRexoFolder = ".rexo/schema.json";

    private const string EmbeddedRexoSchemaResourceName = "Rexo.Configuration.rexo.schema.1.0.json";
    private const string EmbeddedPolicySchemaResourceName = "Rexo.Configuration.policy.schema.1.0.json";

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
        ValidatePolicyMetadata(policyPath, policyJson);
        await ValidatePolicySchemaAsync(policyPath, policyJson, cancellationToken);
        return JsonSerializer.Deserialize<PolicyConfig>(policyJson, JsonOptions);
    }

    public static async Task<string> ReadEmbeddedRexoSchemaJsonAsync(CancellationToken cancellationToken)
    {
        var asm = typeof(RepoConfigurationLoader).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedRexoSchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{EmbeddedRexoSchemaResourceName}' was not found in the assembly.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static async Task<string> ReadEmbeddedPolicySchemaJsonAsync(CancellationToken cancellationToken)
    {
        var asm = typeof(RepoConfigurationLoader).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedPolicySchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{EmbeddedPolicySchemaResourceName}' was not found in the assembly.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
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
            SupportedRexoSchemaUri,
            SupportedRexoSchemaPath,
            "./" + SupportedRexoSchemaPath,
            "../" + SupportedRexoSchemaPath,
            SupportedRexoSchemaPathInRexoFolder,
            LegacySchemaUri,
            LegacySchemaPath,
            "./" + LegacySchemaPath,
            "../" + LegacySchemaPath,
            LegacySchemaPathInRexoFolder,
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

        string schemaJson;
        JsonSchema schema;
        var schemaPath = FindLocalSchemaPath(configDirectory, SupportedRexoSchemaPath, LegacySchemaPath);
        if (schemaPath is not null)
        {
            schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            schema = await ParseSchemaWithCompatibilityAsync(schemaJson, cancellationToken);
        }
        else
        {
            // Fall back to the embedded schema so the tool works when installed globally.
            schemaJson = await ReadEmbeddedRexoSchemaJsonAsync(cancellationToken);
            schema = await ParseSchemaWithCompatibilityAsync(schemaJson, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var errors = ValidateWithCompatibilityFallback(schema, schemaJson, jsonText, cancellationToken);
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

    private static void ValidatePolicyMetadata(string policyPath, string jsonText)
    {
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$schema", out var schemaProp) || schemaProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Policy must define a '$schema' string property.");
        }

        var schema = schemaProp.GetString();
        var allowedSchemaValues = new[]
        {
            SupportedPolicySchemaUri,
            SupportedPolicySchemaPath,
            "./" + SupportedPolicySchemaPath,
            "../" + SupportedPolicySchemaPath,
            SupportedPolicySchemaPathInRexoFolder,
        };

        if (string.IsNullOrWhiteSpace(schema) || !allowedSchemaValues.Contains(schema, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported policy '$schema' value '{schema}'. Expected one of: {string.Join(", ", allowedSchemaValues)}");
        }

        if (!root.TryGetProperty("schemaVersion", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Policy must define a 'schemaVersion' string property.");
        }

        var version = versionProp.GetString();
        if (!string.Equals(version, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported policy schemaVersion '{version}'. Supported version is '{SupportedSchemaVersion}'.");
        }
    }

    private static async Task ValidatePolicySchemaAsync(string policyPath, string jsonText, CancellationToken cancellationToken)
    {
        var policyDirectory = Path.GetDirectoryName(policyPath)
            ?? throw new InvalidOperationException("Could not determine policy directory.");

        cancellationToken.ThrowIfCancellationRequested();

        string schemaJson;
        JsonSchema schema;
        var schemaPath = FindLocalSchemaPath(policyDirectory, SupportedPolicySchemaPath);
        if (schemaPath is not null)
        {
            schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            schema = await ParseSchemaWithCompatibilityAsync(schemaJson, cancellationToken);
        }
        else
        {
            schemaJson = await ReadEmbeddedPolicySchemaJsonAsync(cancellationToken);
            schema = await ParseSchemaWithCompatibilityAsync(schemaJson, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var errors = ValidateWithCompatibilityFallback(schema, schemaJson, jsonText, cancellationToken);
        if (errors.Count > 0)
        {
            var details = string.Join(
                Environment.NewLine,
                errors.Select(e => $"- {e.Path}: {e.Kind} ({e.Property})"));

            throw new InvalidOperationException(
                "Policy does not match the declared schema. Validation errors:" +
                Environment.NewLine +
                details);
        }
    }

    private static string? FindLocalSchemaPath(string baseDirectory, string schemaFileName, string? legacySchemaFileName = null)
    {
        var candidates = new List<string>
        {
            Path.Combine(baseDirectory, schemaFileName),
            Path.Combine(baseDirectory, "..", schemaFileName),
            Path.Combine(baseDirectory, ".rexo", schemaFileName),
        };

        if (!string.IsNullOrWhiteSpace(legacySchemaFileName))
        {
            candidates.Add(Path.Combine(baseDirectory, legacySchemaFileName));
            candidates.Add(Path.Combine(baseDirectory, "..", legacySchemaFileName));
            candidates.Add(Path.Combine(baseDirectory, ".rexo", legacySchemaFileName));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static ICollection<NJsonSchema.Validation.ValidationError> ValidateWithCompatibilityFallback(
        JsonSchema schema,
        string schemaJson,
        string jsonText,
        CancellationToken cancellationToken)
    {
        try
        {
            return schema.Validate(jsonText);
        }
        catch (Exception ex) when (NeedsDefsCompatibilityFallback(ex.Message))
        {
            var normalizedSchemaJson = NormalizeSchemaForLegacyDefinitions(schemaJson);
            var normalizedSchema = JsonSchema.FromJsonAsync(normalizedSchemaJson, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            cancellationToken.ThrowIfCancellationRequested();
            return normalizedSchema.Validate(jsonText);
        }
    }

    private static async Task<JsonSchema> ParseSchemaWithCompatibilityAsync(
        string schemaJson,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSchema.FromJsonAsync(schemaJson, CancellationToken.None);
        }
        catch (Exception ex) when (NeedsDefsCompatibilityFallback(ex.Message))
        {
            var normalizedSchemaJson = NormalizeSchemaForLegacyDefinitions(schemaJson);
            return await JsonSchema.FromJsonAsync(normalizedSchemaJson, CancellationToken.None);
        }
    }

    private static bool NeedsDefsCompatibilityFallback(string? message) =>
        !string.IsNullOrWhiteSpace(message) &&
        message.Contains("#/$defs/", StringComparison.Ordinal);

    private static string NormalizeSchemaForLegacyDefinitions(string schemaJson)
    {
        // NJsonSchema may require draft-07 style definitions in some execution paths.
        return schemaJson
            .Replace("\"$defs\"", "\"definitions\"", StringComparison.Ordinal)
            .Replace("#/$defs/", "#/definitions/", StringComparison.Ordinal)
            .Replace(
                "https://json-schema.org/draft/2020-12/schema",
                "http://json-schema.org/draft-07/schema#",
                StringComparison.Ordinal);
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
