namespace Rexo.Execution;

using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Git;

public static class BuiltinCommandRegistration
{
    public static CommandRegistry CreateDefault(RepoConfig? config = null)
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
                    lines.Add($"    --{optName} ({optCfg.Type}){def}");
                }
            }

            if (cmd.Steps.Count > 0)
            {
                lines.Add("  Steps:");
                foreach (var step in cmd.Steps)
                {
                    var stepDesc = step switch
                    {
                        { Run: not null } => $"run: {step.Run}",
                        { Uses: not null } => $"uses: {step.Uses}",
                        { Command: not null } => $"command: {step.Command}",
                        _ => "unknown",
                    };
                    var when = step.When is not null ? $" (when: {step.When})" : string.Empty;
                    var id = step.Id is not null ? $"[{step.Id}] " : string.Empty;
                    lines.Add($"    {id}{stepDesc}{when}");
                }
            }

            return CommandResult.Ok("explain", string.Join("\n", lines));
        }

        return CommandResult.Fail("explain", 8, $"Command '{commandName}' not found.");
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

