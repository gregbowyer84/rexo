namespace Rexo.Execution;

using System.Net.Http;
using System.Text.Json;
using Rexo.Configuration;
using Rexo.Ci;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Git;
using Rexo.Policies;

public static class BuiltinCommandRegistration
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly HttpClient HttpClient = new();
    private static readonly string[] InitLocationChoices = [".rexo", "root"];
    private static readonly string[] InitTemplateChoices = ["auto", "dotnet", "node", "generic"];
    private static readonly string[] InitYesNoChoices = ["yes", "no"];
    private const string DefaultInstructionsPath = ".github/instructions/rexo.instructions.md";
    private const string InstructionsTemplateUrl = "https://raw.githubusercontent.com/agile-north/rexo/release/next/docs/rexo.instructions.md";

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

        registry.Register("init", async (invocation, ct) =>
            await RunInitAsync(invocation, ct));

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

        // config file
        var configPath = ConfigFileLocator.FindConfigPath(invocation.WorkingDirectory);
        checks.Add((
            "config",
            configPath is not null,
            configPath is not null ? $"found ({Path.GetFileName(configPath)})" : "not found (expected rexo.json/rexo.yml in root or .rexo/)"));

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
        lines.Add("  init            Create a starter rexo config");
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
        var builtins = new[] { "version", "list", "explain", "doctor", "init", "run", "help", "ui" };
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
            return CommandResult.Fail("config resolved", 1, "No rexo configuration loaded.");
        }

        var options = IndentedJsonOptions;
        var json = JsonSerializer.Serialize(config, options);
        return CommandResult.Ok("config resolved", json);
    }

    private static CommandResult RunConfigSources(CommandInvocation invocation, string? configPath)
    {
        var lines = new List<string> { "Configuration sources (in merge order):" };

        var resolvedPath = configPath ?? ConfigFileLocator.GetDefaultConfigPath(invocation.WorkingDirectory);
        var exists = File.Exists(resolvedPath);
        lines.Add($"  [{(exists ? "loaded" : "not found")}] {resolvedPath}");

        var policyPath = ConfigFileLocator.FindPolicyPath(invocation.WorkingDirectory);
        if (policyPath is not null)
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
            return CommandResult.Fail("config materialize", 1, "No rexo configuration loaded.");
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
                "No versioning configuration found in rexo configuration.\n" +
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

    private static async Task<CommandResult> RunInitAsync(
        CommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        var workingDir = invocation.WorkingDirectory;
        var options = invocation.Options;

        var force = IsTrue(options, "force");
        var nonInteractive = IsTrue(options, "yes") || IsTrue(options, "non-interactive");

        var existingConfig = ConfigFileLocator.FindConfigPath(workingDir);
        if (existingConfig is not null && !force)
        {
            return CommandResult.Fail(
                "init",
                1,
                $"Configuration already exists at '{existingConfig}'. Use --force to overwrite.");
        }

        var detectedTemplate = DetectTemplate(workingDir);
        var location = ReadOption(options, "location") ?? ".rexo";
        var template = ReadOption(options, "template") ?? detectedTemplate;
        var withPolicy = IsTrue(options, "with-policy");
        var policyTemplate = ReadOption(options, "policy-template");
        var instructionsPathOption = ReadOption(options, "instructions-path");
        var withInstructions = IsTrue(options, "with-instructions") || !string.IsNullOrWhiteSpace(instructionsPathOption);

        if (!nonInteractive)
        {
            Console.WriteLine("Rexo init");
            Console.WriteLine($"Detected repository template: {detectedTemplate}");

            location = PromptChoice(
                "Where should config be created?",
                InitLocationChoices,
                location.Equals("root", StringComparison.OrdinalIgnoreCase) ? "root" : ".rexo");

            template = PromptChoice(
                "Choose starter template:",
                InitTemplateChoices,
                "auto");

            if (template.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                template = detectedTemplate;
            }

            var createPolicyAnswer = PromptChoice(
                "Create a starter policy file?",
                InitYesNoChoices,
                "yes");
            withPolicy = createPolicyAnswer.Equals("yes", StringComparison.OrdinalIgnoreCase);

            var createInstructionsAnswer = PromptChoice(
                "Download AI instructions file into this repo?",
                InitYesNoChoices,
                "no");
            withInstructions = createInstructionsAnswer.Equals("yes", StringComparison.OrdinalIgnoreCase);

            if (withPolicy)
            {
                var available = EmbeddedPolicyTemplates.TemplateNames;
                var defaultPolicyTemplate = available.Contains(template, StringComparer.OrdinalIgnoreCase)
                    ? template
                    : available.Contains("standard", StringComparer.OrdinalIgnoreCase)
                        ? "standard"
                        : available.Count > 0 ? available[0] : "standard";

                policyTemplate = PromptChoice(
                    "Choose policy template:",
                    available,
                    defaultPolicyTemplate);
            }

            if (withInstructions)
            {
                instructionsPathOption ??= DefaultInstructionsPath;
                Console.Write($"Instructions path [{instructionsPathOption}] > ");
                var inputPath = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(inputPath))
                {
                    instructionsPathOption = inputPath;
                }
            }
        }
        else if (template.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            template = detectedTemplate;
        }

        if (withPolicy && string.IsNullOrWhiteSpace(policyTemplate))
        {
            var available = EmbeddedPolicyTemplates.TemplateNames;
            policyTemplate = available.Contains(template, StringComparer.OrdinalIgnoreCase)
                ? template
                : available.Contains("standard", StringComparer.OrdinalIgnoreCase)
                    ? "standard"
                    : available.Count > 0 ? available[0] : null;
        }

        location = NormalizeLocation(location);
        if (location is null)
        {
            return CommandResult.Fail("init", 1, "Invalid --location value. Use '.rexo' or 'root'.");
        }

        template = NormalizeTemplate(template);
        if (template is null)
        {
            return CommandResult.Fail("init", 1, "Invalid --template value. Use auto|dotnet|node|generic.");
        }

        if (withPolicy && string.IsNullOrWhiteSpace(policyTemplate))
        {
            return CommandResult.Fail("init", 1, "No policy templates are available to initialize.");
        }

        if (withPolicy && !EmbeddedPolicyTemplates.TemplateNames.Contains(policyTemplate!, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.Fail(
                "init",
                1,
                $"Invalid --policy-template value '{policyTemplate}'. Available: {string.Join(", ", EmbeddedPolicyTemplates.TemplateNames)}");
        }

        var configDir = location == ".rexo"
            ? Path.Combine(workingDir, ".rexo")
            : workingDir;

        string? instructionsTargetPath = null;
        if (withInstructions)
        {
            var relativeInstructionsPath = string.IsNullOrWhiteSpace(instructionsPathOption)
                ? DefaultInstructionsPath
                : instructionsPathOption;

            if (Path.IsPathRooted(relativeInstructionsPath))
            {
                return CommandResult.Fail("init", 1, "Invalid --instructions-path value. Use a repository-relative path.");
            }

            instructionsTargetPath = Path.GetFullPath(Path.Combine(workingDir, relativeInstructionsPath));
            var repoRoot = Path.GetFullPath(workingDir + Path.DirectorySeparatorChar);
            if (!instructionsTargetPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            {
                return CommandResult.Fail("init", 1, "Invalid --instructions-path value. Path must remain within the repository.");
            }

            if (File.Exists(instructionsTargetPath) && !force)
            {
                return CommandResult.Fail(
                    "init",
                    1,
                    $"Instructions file already exists at '{instructionsTargetPath}'. Use --force to overwrite.");
            }
        }

        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "rexo.json");
        if (File.Exists(configPath) && !force)
        {
            return CommandResult.Fail("init", 1, $"Target config already exists at '{configPath}'. Use --force to overwrite.");
        }

        var repoName = Path.GetFileName(workingDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configJson = BuildStarterConfigJson(repoName, template);

        await File.WriteAllTextAsync(configPath, configJson, cancellationToken);

        string? policyPath = null;
        if (withPolicy)
        {
            policyPath = Path.Combine(configDir, "policy.json");
            if (File.Exists(policyPath) && !force)
            {
                return CommandResult.Fail(
                    "init",
                    1,
                    $"Target policy already exists at '{policyPath}'. Use --force to overwrite.");
            }

            var policyJson = EmbeddedPolicyTemplates.ReadTemplate(policyTemplate!);
            await File.WriteAllTextAsync(policyPath, policyJson, cancellationToken);
        }

        if (withInstructions)
        {
            var instructionsDirectory = Path.GetDirectoryName(instructionsTargetPath!);
            if (!string.IsNullOrWhiteSpace(instructionsDirectory))
            {
                Directory.CreateDirectory(instructionsDirectory);
            }

            string instructionsContent;
            try
            {
                instructionsContent = await HttpClient.GetStringAsync(InstructionsTemplateUrl, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                return CommandResult.Fail("init", 1, $"Failed to download instructions template: {ex.Message}");
            }

            await File.WriteAllTextAsync(instructionsTargetPath!, instructionsContent, cancellationToken);
        }

        var lines = new List<string>
        {
            $"Initialized Rexo config: {configPath}",
            $"Template: {template}",
            withPolicy ? $"Policy template: {policyTemplate}" : "Policy template: none",
            withPolicy ? $"Initialized policy: {policyPath}" : "Policy file: not created",
            withInstructions ? $"Initialized instructions: {instructionsTargetPath}" : "Instructions file: not created",
            "Next steps:",
            "  1. Review and edit rexo.json for your workflow.",
            "  2. Run 'rx list' and then 'rx build' (or your configured command).",
            "  Docs: https://github.com/agile-north/rexo/blob/release/next/docs/CONFIGURATION.md",
        };

        return CommandResult.Ok("init", string.Join(Environment.NewLine, lines));
    }

    private static string DetectTemplate(string workingDir)
    {
        if (Directory.EnumerateFiles(workingDir, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
            Directory.EnumerateFiles(workingDir, "*.csproj", SearchOption.AllDirectories).Any())
        {
            return "dotnet";
        }

        if (File.Exists(Path.Combine(workingDir, "package.json")))
        {
            return "node";
        }

        return "generic";
    }

    private static string? NormalizeLocation(string value)
    {
        if (value.Equals(".rexo", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("rexo", StringComparison.OrdinalIgnoreCase))
        {
            return ".rexo";
        }

        if (value.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return "root";
        }

        return null;
    }

    private static string? NormalizeTemplate(string value)
    {
        var known = new[] { "dotnet", "node", "generic" };
        return known.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value.ToLowerInvariant()
            : null;
    }

    private static bool IsTrue(IReadOnlyDictionary<string, string?> options, string key)
    {
        if (!options.TryGetValue(key, out var value)) return false;
        return string.IsNullOrEmpty(value) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadOption(IReadOnlyDictionary<string, string?> options, string key) =>
        options.TryGetValue(key, out var value) ? value : null;

    private static string PromptChoice(string prompt, IReadOnlyList<string> choices, string defaultChoice)
    {
        Console.WriteLine(prompt);
        Console.WriteLine($"  Choices: {string.Join(", ", choices)}");
        Console.Write($"  [{defaultChoice}] > ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return defaultChoice;
        }

        var match = choices.FirstOrDefault(c => c.Equals(input, StringComparison.OrdinalIgnoreCase));
        return match ?? defaultChoice;
    }

    private static string BuildStarterConfigJson(string repoName, string template)
    {
        object commands = template switch
        {
            "dotnet" => new Dictionary<string, object>
            {
                ["build"] = new
                {
                    description = "Build the solution",
                    options = new Dictionary<string, object>(),
                    steps = new object[]
                    {
                        new { id = "restore", run = "dotnet restore" },
                        new { id = "build", run = "dotnet build -c Release --no-restore" },
                    },
                },
                ["test"] = new
                {
                    description = "Run tests",
                    options = new Dictionary<string, object>(),
                    steps = new object[]
                    {
                        new { run = "dotnet test -c Release --no-build" },
                    },
                },
            },
            "node" => new Dictionary<string, object>
            {
                ["build"] = new
                {
                    description = "Install and build",
                    options = new Dictionary<string, object>(),
                    steps = new object[]
                    {
                        new { run = "npm ci" },
                        new { run = "npm run build" },
                    },
                },
                ["test"] = new
                {
                    description = "Run tests",
                    options = new Dictionary<string, object>(),
                    steps = new object[]
                    {
                        new { run = "npm test" },
                    },
                },
            },
            _ => new Dictionary<string, object>
            {
                ["build"] = new
                {
                    description = "Starter build command",
                    options = new Dictionary<string, object>(),
                    steps = new object[]
                    {
                        new { run = "echo TODO: replace with real build command" },
                    },
                },
            },
        };

        var doc = new Dictionary<string, object?>
        {
            ["$schema"] = "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/schema.json",
            ["schemaVersion"] = "1.0",
            ["name"] = string.IsNullOrWhiteSpace(repoName) ? "my-repo" : repoName,
            ["description"] = "Generated by rx init",
            ["commands"] = commands,
            ["aliases"] = new Dictionary<string, string>(),
        };

        return JsonSerializer.Serialize(doc, IndentedJsonOptions);
    }
}

