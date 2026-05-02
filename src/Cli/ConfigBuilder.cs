namespace Rexo.Cli;

using System.Text.Json;
using Rexo.Configuration;
using Rexo.Configuration.Models;
using Rexo.Policies;

/// <summary>
/// Handles config and policy loading, parsing, and merging.
/// Manages embedded policies, local policies, and effective config construction.
/// </summary>
internal static class ConfigBuilder
{
    public static async Task<RepoConfig?> LoadConfigAsync(string workingDir, bool debug, CancellationToken cancellationToken)
    {
        var configPath = ConfigFileLocator.FindConfigPath(workingDir)
            ?? ConfigFileLocator.GetDefaultConfigPath(workingDir);

        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var config = await RepoConfigurationLoader.LoadAsync(configPath, cancellationToken);
            if (debug) Console.WriteLine($"[debug] Loaded config: {configPath} ({config.Name})");
            return config;
        }
        catch (Exception ex)
        {
            Ui.ConsoleRenderer.RenderError($"Failed to load config '{configPath}': {ex.Message}");
            return null;
        }
    }

    public static async Task<PolicyConfig> LoadAndMergePoliciesAsync(RepoConfig config, string workingDir, bool debug, CancellationToken cancellationToken)
    {
        var embeddedPolicy = LoadEmbeddedPolicyTemplate("standard", debug);
        var remotePolicies = await PolicySourceLoader.LoadPoliciesFromEnvironmentAsync(workingDir, debug, cancellationToken);
        var localPolicy = await LoadLocalPolicyAsync(workingDir, debug, cancellationToken);
        return MergePolicies(MergePolicies(embeddedPolicy, remotePolicies), localPolicy);
    }

    private static async Task<PolicyConfig> LoadLocalPolicyAsync(string workingDir, bool debug, CancellationToken cancellationToken)
    {
        var policyPath = ConfigFileLocator.FindPolicyPath(workingDir);
        if (policyPath is null)
        {
            return new PolicyConfig();
        }

        try
        {
            var policyConfig = await RepoConfigurationLoader.LoadPolicyAsync(policyPath, cancellationToken);
            if (debug && policyConfig is not null) Console.WriteLine($"[debug] Loaded policy: {policyPath}");
            return policyConfig ?? new PolicyConfig();
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine($"[debug] Policy load skipped ({policyPath}): {ex.Message}");
            return new PolicyConfig();
        }
    }

    public static PolicyConfig MergePolicies(PolicyConfig? baseline, PolicyConfig? overridePolicy)
    {
        var commands = new Dictionary<string, RepoCommandConfig>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (baseline?.Commands is { Count: > 0 })
        {
            foreach (var (name, command) in baseline.Commands)
            {
                commands[name] = NormalizeCommandConfig(command);
            }
        }

        if (overridePolicy?.Commands is { Count: > 0 })
        {
            foreach (var (name, command) in overridePolicy.Commands)
            {
                commands[name] = NormalizeCommandConfig(command);
            }
        }

        if (baseline?.Aliases is { Count: > 0 })
        {
            foreach (var (alias, target) in baseline.Aliases)
            {
                aliases[alias] = target;
            }
        }

        if (overridePolicy?.Aliases is { Count: > 0 })
        {
            foreach (var (alias, target) in overridePolicy.Aliases)
            {
                aliases[alias] = target;
            }
        }

        return new PolicyConfig(commands, aliases);
    }

    public static RepoConfig? MergePolicyIntoEffectiveConfig(RepoConfig? config, PolicyConfig? policy)
    {
        if (config is null)
        {
            return null;
        }

        if (policy is null)
        {
            return config;
        }

        var commands = new Dictionary<string, RepoCommandConfig>(StringComparer.OrdinalIgnoreCase);
        if (policy.Commands is not null)
        {
            foreach (var (name, command) in policy.Commands)
            {
                commands[name] = NormalizeCommandConfig(command);
            }
        }

        foreach (var (name, command) in config.Commands ?? [])
        {
            commands[name] = NormalizeCommandConfig(command);
        }

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (policy.Aliases is not null)
        {
            foreach (var (alias, target) in policy.Aliases)
            {
                aliases[alias] = target;
            }
        }

        foreach (var (alias, target) in config.Aliases ?? [])
        {
            aliases[alias] = target;
        }

        return config with
        {
            Commands = commands,
            Aliases = aliases,
        };
    }

    private static PolicyConfig LoadEmbeddedPolicyTemplate(string templateName, bool debug)
    {
        try
        {
            var json = EmbeddedPolicyTemplates.ReadTemplate(templateName);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var commands = new Dictionary<string, RepoCommandConfig>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("commands", out var commandsElement) && commandsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var commandProperty in commandsElement.EnumerateObject())
                {
                    var command = JsonSerializer.Deserialize<RepoCommandConfig>(commandProperty.Value.GetRawText());
                    if (command is not null)
                    {
                        commands[commandProperty.Name] = NormalizeCommandConfig(command);
                    }
                }
            }

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("aliases", out var aliasesElement) && aliasesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var aliasProperty in aliasesElement.EnumerateObject())
                {
                    var value = aliasProperty.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        aliases[aliasProperty.Name] = value;
                    }
                }
            }

            if (debug)
            {
                Console.WriteLine($"[debug] Loaded embedded policy template: {templateName}");
            }

            return new PolicyConfig(commands, aliases);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            if (debug)
            {
                Console.WriteLine($"[debug] Embedded policy '{templateName}' load failed: {ex.Message}");
            }

            return new PolicyConfig();
        }
    }

    private static RepoCommandConfig NormalizeCommandConfig(RepoCommandConfig command) =>
        new(
            command.Description,
            command.Options ?? [],
            command.Steps ?? [])
        {
            Args = command.Args ?? [],
            MaxParallel = command.MaxParallel,
        };
}
