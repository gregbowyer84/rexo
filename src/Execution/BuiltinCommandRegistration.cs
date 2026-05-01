namespace Rexo.Execution;

using System.Text.Json;
using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Git;

public static class BuiltinCommandRegistration
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    public static CommandRegistry CreateDefault(RepoConfig? config = null, string? configPath = null)
    {
        var registry = new CommandRegistry();

        registry.Register("version", (_, _) =>
            Task.FromResult(CommandResult.Ok("version", GetVersion())));

        registry.Register("doctor", async (invocation, ct) =>
            await RunDoctorAsync(invocation, config, ct));

        registry.Register("list", (invocation, _) =>
            Task.FromResult(RunList(invocation, registry, config)));

        registry.Register("explain", (invocation, ct) =>
            Task.FromResult(RunExplain(invocation, config)));

        registry.Register("config resolved", (_, _) =>
            Task.FromResult(RunConfigResolved(config)));

        registry.Register("config sources", (invocation, _) =>
            Task.FromResult(RunConfigSources(invocation, configPath)));

        registry.Register("config materialize", async (invocation, ct) =>
            await RunConfigMaterializeAsync(invocation, config, ct));

        registry.Register("explain version", (_, _) =>
            Task.FromResult(RunExplainVersion(config)));

        return registry;
    }

    private static string GetVersion()
    {
        var assembly = typeof(BuiltinCommandRegistration).Assembly;
        var infoVersion = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        return infoVersion ?? assembly.GetName().Version?.ToString() ?? "0.1.0-local";
    }

    private static async Task<CommandResult> RunDoctorAsync(
        CommandInvocation invocation,
        RepoConfig? config,
        CancellationToken cancellationToken)
    {
        var checks = new List<(string Name, bool Passed, string? Detail)>();

        // Git
        var gitInfo = await GitDetector.DetectAsync(invocation.WorkingDirectory, cancellationToken);
        checks.Add(("git", gitInfo.Branch is not null, gitInfo.Branch ?? "not a git repo or git not found"));

        // dotnet
        var dotnetOk = await IsToolAvailableAsync("dotnet", "--version", cancellationToken);
        checks.Add(("dotnet", dotnetOk.ok, dotnetOk.version));

        // docker (only if docker artifacts configured)
        var needsDocker = config?.Artifacts?.Any(a => a.Type == "docker") == true;
        if (needsDocker)
        {
            var dockerOk = await IsToolAvailableAsync("docker", "--version", cancellationToken);
            checks.Add(("docker", dockerOk.ok, dockerOk.version));
        }

        // repo.json
        var configPath = Path.Combine(invocation.WorkingDirectory, "repo.json");
        checks.Add(("repo.json", File.Exists(configPath), File.Exists(configPath) ? "found" : "not found"));

        // CI context
        var ciInfo = CiDetector.Detect();
        if (ciInfo.IsCi)
        {
            checks.Add(("ci", true, $"Running in CI ({ciInfo.Provider})"));
        }

        var allPassed = checks.All(c => c.Passed);
        var lines = checks.Select(c => $"  [{(c.Passed ? "OK" : "FAIL")}] {c.Name}: {c.Detail ?? "ok"}");
        var message = $"Doctor results:\n{string.Join("\n", lines)}";

        return new CommandResult("doctor", allPassed, allPassed ? 0 : 9, message,
            new Dictionary<string, object?>());
    }

    private static CommandResult RunList(
        CommandInvocation invocation,
        CommandRegistry registry,
        RepoConfig? config)
    {
        var lines = new List<string>();
        lines.Add("Built-in commands:");
        lines.Add("  version         Show the version of repo");
        lines.Add("  list            List all available commands");
        lines.Add("  explain         Explain a command");
        lines.Add("  doctor          Check environment and configuration");
        lines.Add("  run <command>   Run a config-defined command");
        lines.Add("  help            Show help");
        lines.Add("  ui              Open the interactive UI");

        if (config is not null && config.Commands.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Config-defined commands:");
            foreach (var (name, cmd) in config.Commands)
            {
                var desc = cmd.Description ?? string.Empty;
                lines.Add($"  {name,-20} {desc}");
            }
        }

        if (config is not null && config.Aliases.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Aliases:");
            foreach (var (alias, target) in config.Aliases)
            {
                lines.Add($"  {alias,-20} -> {target}");
            }
        }

        return CommandResult.Ok("list", string.Join("\n", lines));
    }

    private static CommandResult RunExplain(CommandInvocation invocation, RepoConfig? config)
    {
        // The command name is passed as the first arg
        if (!invocation.Args.TryGetValue("command", out var commandName) || string.IsNullOrEmpty(commandName))
        {
            return CommandResult.Fail("explain", 1, "Usage: repo explain <command>");
        }

        // Check built-ins
        var builtins = new[] { "version", "list", "explain", "doctor", "run", "help", "ui" };
        if (builtins.Contains(commandName, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.Ok("explain", $"Built-in command: {commandName}\n  This is a built-in command that is always available.");
        }

        // Check config commands
        if (config?.Commands.TryGetValue(commandName, out var cmd) == true && cmd is not null)
        {
            var lines = new List<string>();
            lines.Add($"Command: {commandName}");
            if (!string.IsNullOrEmpty(cmd.Description))
                lines.Add($"  Description: {cmd.Description}");

            if (cmd.MaxParallel.HasValue)
                lines.Add($"  Max parallel steps: {cmd.MaxParallel.Value}");

            if (cmd.Args is { Count: > 0 })
            {
                lines.Add("  Arguments:");
                foreach (var (argName, argCfg) in cmd.Args)
                {
                    var req = argCfg.Required ? "required" : "optional";
                    lines.Add($"    {argName} ({req}): {argCfg.Description ?? string.Empty}");
                }
            }

            if (cmd.Options.Count > 0)
            {
                lines.Add("  Options:");
                foreach (var (optName, optCfg) in cmd.Options)
                {
                    var def = optCfg.Default is not null ? $" [default: {optCfg.Default}]" : string.Empty;
                    var allowed = optCfg.Allowed is { Length: > 0 }
                        ? $" [allowed: {string.Join(", ", optCfg.Allowed)}]"
                        : string.Empty;
                    lines.Add($"    --{optName} ({optCfg.Type}){def}{allowed}");
                }
            }

            if (cmd.Steps.Count > 0)
            {
                lines.Add($"  Steps ({cmd.Steps.Count} total):");
                var stepIndex = 0;
                foreach (var step in cmd.Steps)
                {
                    stepIndex++;
                    var stepType = step switch
                    {
                        { Run: not null } => "run",
                        { Uses: not null } => "uses",
                        { Command: not null } => "command",
                        _ => "unknown",
                    };
                    var stepBody = step switch
                    {
                        { Run: not null } => step.Run,
                        { Uses: not null } => step.Uses,
                        { Command: not null } => step.Command,
                        _ => string.Empty,
                    };
                    var id = step.Id is not null ? $"[{step.Id}] " : $"[step-{stepIndex}] ";
                    var desc = step.Description is not null ? $" — {step.Description}" : string.Empty;
                    lines.Add($"    {id}{stepType}: {stepBody}{desc}");

                    if (step.When is not null)
                        lines.Add($"        when: {step.When}");
                    if (step.Parallel == true)
                        lines.Add($"        parallel: true");
                    if (step.DependsOn is { Length: > 0 })
                        lines.Add($"        dependsOn: {string.Join(", ", step.DependsOn)}");
                    if (step.ContinueOnError == true)
                        lines.Add($"        continueOnError: true");
                    if (step.OutputPattern is not null)
                        lines.Add($"        outputPattern: {step.OutputPattern}");
                    if (step.OutputFile is not null)
                        lines.Add($"        outputFile: {step.OutputFile}");
                }
            }

            // Push eligibility information
            if (config.PushRulesJson is not null)
            {
                lines.Add("  Push rules:");
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(config.PushRulesJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        lines.Add($"    {prop.Name}: {prop.Value}");
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    lines.Add("    (unable to parse push rules)");
                }
            }

            // Version provider info
            if (config.Versioning is not null)
            {
                lines.Add($"  Version provider: {config.Versioning.Provider}");
                if (config.Versioning.Fallback is not null)
                    lines.Add($"    Fallback: {config.Versioning.Fallback}");
            }

            return CommandResult.Ok("explain", string.Join("\n", lines));
        }

        return CommandResult.Fail("explain", 8, $"Command '{commandName}' not found.");
    }

    private static CommandResult RunConfigResolved(RepoConfig? config)
    {
        if (config is null)
        {
            return CommandResult.Fail("config resolved", 1, "No repo.json configuration loaded.");
        }

        var options = IndentedJsonOptions;
        var json = JsonSerializer.Serialize(config, options);
        return CommandResult.Ok("config resolved", json);
    }

    private static CommandResult RunConfigSources(CommandInvocation invocation, string? configPath)
    {
        var lines = new List<string> { "Configuration sources (in merge order):" };

        var resolvedPath = configPath ?? Path.Combine(invocation.WorkingDirectory, "repo.json");
        var exists = File.Exists(resolvedPath);
        lines.Add($"  [{(exists ? "loaded" : "not found")}] {resolvedPath}");

        var policyPath = Path.Combine(invocation.WorkingDirectory, "policy.json");
        if (File.Exists(policyPath))
        {
            lines.Add($"  [policy] {policyPath}");
        }

        var overlayEnvPath = Environment.GetEnvironmentVariable("REXO_OVERLAY");
        if (!string.IsNullOrEmpty(overlayEnvPath))
        {
            lines.Add($"  [overlay] REXO_OVERLAY={overlayEnvPath}");
        }

        return CommandResult.Ok("config sources", string.Join("\n", lines));
    }

    private static async Task<CommandResult> RunConfigMaterializeAsync(
        CommandInvocation invocation,
        RepoConfig? config,
        CancellationToken cancellationToken)
    {
        if (config is null)
        {
            return CommandResult.Fail("config materialize", 1, "No repo.json configuration loaded.");
        }

        var materialized = new List<string>();
        var workingDir = invocation.WorkingDirectory;

        // If using gitversion provider, write GitVersion.yml if absent
        if (string.Equals(config.Versioning?.Provider, "gitversion", StringComparison.OrdinalIgnoreCase))
        {
            var gvPath = Path.Combine(workingDir, "GitVersion.yml");
            if (!File.Exists(gvPath))
            {
                const string gvContent = """
                    mode: ContinuousDeployment
                    branches: {}
                    ignore:
                      sha: []
                    """;
                await File.WriteAllTextAsync(gvPath, gvContent, cancellationToken);
                materialized.Add(gvPath);
                Console.WriteLine($"  Materialized: {gvPath}");
            }
        }

        var message = materialized.Count > 0
            ? $"Materialized {materialized.Count} file(s): {string.Join(", ", materialized)}"
            : "Nothing to materialize.";

        return CommandResult.Ok("config materialize", message);
    }

    private static CommandResult RunExplainVersion(RepoConfig? config)
    {
        if (config?.Versioning is null)
        {
            return CommandResult.Ok("explain version",
                "No versioning configuration found in repo.json.\n" +
                "Available providers: fixed, env, git, gitversion, minver, nbgv");
        }

        var v = config.Versioning;
        var lines = new List<string>
        {
            "Versioning configuration:",
            $"  Provider:  {v.Provider}",
        };

        if (!string.IsNullOrEmpty(v.Fallback))
            lines.Add($"  Fallback:  {v.Fallback}");

        if (v.Settings is { Count: > 0 })
        {
            lines.Add("  Settings:");
            foreach (var (k, val) in v.Settings)
                lines.Add($"    {k}: {val}");
        }

        lines.Add(string.Empty);
        lines.Add("Available providers: fixed, env, git, gitversion, minver, nbgv");

        return CommandResult.Ok("explain version", string.Join("\n", lines));
    }

    private static async Task<(bool ok, string? version)> IsToolAvailableAsync(
        string tool,
        string versionArg,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(tool, versionArg)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null) return (false, null);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return (process.ExitCode == 0, output.Trim().Split('\n')[0].Trim());
        }
        catch (Exception)
        {
            return (false, "not found");
        }
    }
}

