namespace Rexo.Cli;

using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Artifacts.Helm;
using Rexo.Artifacts.Docker;
using Rexo.Artifacts.NuGet;
using Rexo.Ci;
using Rexo.Configuration;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Execution;
using Rexo.Git;
using Rexo.Policies;
using Rexo.Templating;
using Rexo.Tui;
using Rexo.Ui;
using Rexo.Versioning;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task<int> Main(string[] args) => ExecuteAsync(args, CancellationToken.None);

    public static async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        var workingDir = Environment.CurrentDirectory;

        // Parse global flags
        var (cleanArgs, json, jsonFile, verbose, debug, quiet, setOverrides) = ParseGlobalFlags(args);

        // No args (or only global flags) — show help
        if (cleanArgs.Count == 0)
        {
            PrintHelp();
            return 0;
        }

        var command = cleanArgs[0];

        // Handle help early
        if (command is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        // Set up the full service graph
        var (registry, executor, config) = await CliBootstrapper.BuildServicesAsync(workingDir, debug, setOverrides, cancellationToken);

        return command switch
        {
            "version" => await RunBuiltinAsync(executor, "version", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "doctor" => await RunBuiltinAsync(executor, "doctor", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "init" => await RunInitBuiltinAsync(executor, cleanArgs, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            "list" => await RunBuiltinAsync(executor, "list", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "explain" => await RunExplainAsync(executor, cleanArgs, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            "config" => await RunConfigSubcommandAsync(cleanArgs, executor, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            "ui" => await RunUiAsync(executor, config, workingDir, cancellationToken),
            "run" => await RunConfiguredAsync(cleanArgs, executor, config, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            _ => await RunDirectAsync(command, cleanArgs, executor, config, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
        };
    }

    private static async Task<(CommandRegistry registry, DefaultCommandExecutor executor, RepoConfig? config)>
        BuildServicesAsync(string workingDir, bool debug, CancellationToken cancellationToken)
    {
        if (debug) Console.WriteLine($"[debug] Loading configuration from {workingDir}");
        // Try load config — check multiple candidate locations in priority order
        RepoConfig? config = null;
        PolicyConfig? policyConfig = null;
        var configPath = ConfigFileLocator.FindConfigPath(workingDir)
            ?? ConfigFileLocator.GetDefaultConfigPath(workingDir);
        if (File.Exists(configPath))
        {
            try
            {
                config = await RepoConfigurationLoader.LoadAsync(configPath, cancellationToken);
                if (debug) Console.WriteLine($"[debug] Loaded config: {configPath} ({config.Name})");
            }
            catch (Exception ex)
            {
                ConsoleRenderer.RenderError($"Failed to load config '{configPath}': {ex.Message}");
            }
        }

        if (config is not null)
        {
            var embeddedPolicy = LoadEmbeddedPolicyTemplate("standard", debug);
            var policyPath = ConfigFileLocator.FindPolicyPath(workingDir);
            if (policyPath is not null)
            {
                try
                {
                    policyConfig = await RepoConfigurationLoader.LoadPolicyAsync(policyPath, cancellationToken);
                    if (debug && policyConfig is not null) Console.WriteLine($"[debug] Loaded policy: {policyPath}");
                }
                catch (Exception ex)
                {
                    if (debug) Console.WriteLine($"[debug] Policy load skipped ({policyPath}): {ex.Message}");
                }
            }

            policyConfig = MergePolicies(embeddedPolicy, policyConfig);
        }

        var effectiveConfig = MergePolicyIntoEffectiveConfig(config, policyConfig);

        // Build registry
        var registry = BuiltinCommandRegistration.CreateDefault(effectiveConfig, File.Exists(configPath) ? configPath : null);
        var executor = new DefaultCommandExecutor(registry);

        if (config is not null)
        {
            // Set up provider registries
            var templateRenderer = new TemplateRenderer();
            var versionProviders = VersionProviderRegistry.CreateDefault();
            var artifactProviders = new ArtifactProviderRegistry();
            artifactProviders.Register("helm-oci", new HelmOciArtifactProvider());
            artifactProviders.Register("docker", new DockerArtifactProvider());
            artifactProviders.Register("nuget", new NuGetArtifactProvider());

            var builtinRegistry = new BuiltinRegistry();
            var configLoader = new ConfigCommandLoader(
                builtinRegistry,
                templateRenderer,
                versionProviders,
                artifactProviders);

            configLoader.LoadInto(registry, config, workingDir, executor);
            configLoader.LoadPolicyCommandsInto(registry, policyConfig ?? new PolicyConfig(), config, workingDir, executor);
        }

        return (registry, executor, effectiveConfig);
    }

    private static RepoConfig? MergePolicyIntoEffectiveConfig(RepoConfig? config, PolicyConfig? policy)
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

    private static PolicyConfig MergePolicies(PolicyConfig? baseline, PolicyConfig? overridePolicy)
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

    private static async Task<int> RunBuiltinAsync(
        DefaultCommandExecutor executor,
        string command,
        CommandInvocation invocation,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteCommandAsync(executor, command, invocation, cancellationToken);
        return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
    }

    private static async Task<int> RunExplainAsync(
        DefaultCommandExecutor executor,
        IReadOnlyList<string> args,
        string workingDir,
        bool json,
        string? jsonFile,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        if (args.Count < 2)
        {
            ConsoleRenderer.RenderError($"Usage: {GetCliCommandName()} explain <command>");
            return 1;
        }

        // Collect multi-word command name: explain branch feature
        var commandName = string.Join(" ", args.Skip(1));
        var invocation = new CommandInvocation(
            new Dictionary<string, string> { ["command"] = commandName },
            new Dictionary<string, string?>(),
            json,
            jsonFile,
            workingDir);

        var result = await ExecuteCommandAsync(executor, "explain", invocation, cancellationToken);
        return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
    }

    private static async Task<int> RunConfigSubcommandAsync(
        IReadOnlyList<string> args,
        DefaultCommandExecutor executor,
        string workingDir,
        bool json,
        string? jsonFile,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        // args[0] == "config", args[1] == sub-command
        if (args.Count < 2)
        {
            Console.WriteLine("Usage: rx config <resolved|sources|materialize>");
            return 1;
        }

        var subCommand = $"config {args[1].ToLowerInvariant()}";
        var invocation = EmptyInvocation(workingDir, json, jsonFile);
        var result = await ExecuteCommandAsync(executor, subCommand, invocation, cancellationToken);
        return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
    }

    private static async Task<int> RunUiAsync(
        DefaultCommandExecutor executor,
        RepoConfig? config,
        string workingDir,
        CancellationToken cancellationToken)
    {
        await RexoTuiHost.RunAsync(executor, config, workingDir, cancellationToken);
        return 0;
    }

    private static async Task<int> RunConfiguredAsync(
        IReadOnlyList<string> args,
        DefaultCommandExecutor executor,
        RepoConfig? config,
        string workingDir,
        bool json,
        string? jsonFile,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        if (args.Count < 2)
        {
            ConsoleRenderer.RenderError($"Usage: {GetCliCommandName()} run <command> [options]");
            return 1;
        }

        // args[0] == "run" — resolve using same longest-match as direct invocation
        var candidateArgs = args.Skip(1).ToList();

        for (var wordCount = candidateArgs.Count; wordCount >= 1; wordCount--)
        {
            var candidateName = string.Join(" ", candidateArgs.Take(wordCount));
            if (!executor.Registry.TryResolve(candidateName, out _)) continue;

            var remainingArgs = candidateArgs.Skip(wordCount).ToList();
            var (parsedArgs, parsedOptions) = ParseArgsAndOptions(remainingArgs);

            // Map positional args to declared arg names when config defines them
            if (config?.Commands?.TryGetValue(candidateName, out var cmdConfig) == true &&
                cmdConfig.Args is { Count: > 0 })
            {
                var argNames = cmdConfig.Args.Keys.ToArray();
                var positionalArgs = remainingArgs
                    .Where(a => !a.StartsWith("--", StringComparison.Ordinal))
                    .ToArray();

                for (var i = 0; i < Math.Min(argNames.Length, positionalArgs.Length); i++)
                {
                    parsedArgs = new Dictionary<string, string>(parsedArgs)
                    {
                        [argNames[i]] = positionalArgs[i]
                    };
                }
            }

            var invocation = new CommandInvocation(parsedArgs, parsedOptions, json, jsonFile, workingDir);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await ExecuteCommandAsync(executor, candidateName, invocation, cancellationToken);
            var completedAt = DateTimeOffset.UtcNow;

            var exitCode = await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);

            // Write run manifest if --json-file specified
            if (!string.IsNullOrEmpty(jsonFile))
            {
                await WriteRunManifestAsync(result, candidateName, workingDir, startedAt, completedAt, jsonFile, cancellationToken);
            }

            return exitCode;
        }

        var requestedCommand = string.Join(" ", candidateArgs);
        ConsoleRenderer.RenderError($"Command '{requestedCommand}' not found. Run '{GetCliCommandName()} list' to see available commands.");
        return 8;
    }

    private static async Task<int> RunInitBuiltinAsync(
        DefaultCommandExecutor executor,
        IReadOnlyList<string> args,
        string workingDir,
        bool json,
        string? jsonFile,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        var remainingArgs = args.Skip(1).ToList();
        string? mode = null;
        if (remainingArgs.Count > 0 && !remainingArgs[0].StartsWith("--", StringComparison.Ordinal))
        {
            mode = remainingArgs[0];
            remainingArgs.RemoveAt(0);
        }

        var (parsedArgs, parsedOptions) = ParseArgsAndOptions(remainingArgs);
        if (!string.IsNullOrWhiteSpace(mode))
        {
            parsedOptions = new Dictionary<string, string?>(parsedOptions)
            {
                ["mode"] = mode,
            };
        }

        var invocation = new CommandInvocation(parsedArgs, parsedOptions, json, jsonFile, workingDir);

        var result = await ExecuteCommandAsync(executor, "init", invocation, cancellationToken);
        return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
    }

    private static async Task<int> RunDirectAsync(
        string command,
        IReadOnlyList<string> args,
        DefaultCommandExecutor executor,
        RepoConfig? config,
        string workingDir,
        bool json,
        string? jsonFile,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        // Try multi-word command resolution: "branch feature" from ["branch", "feature", "name"]
        // Try progressively shorter command names
        for (var wordCount = args.Count; wordCount >= 1; wordCount--)
        {
            var candidateName = string.Join(" ", args.Take(wordCount));
            if (!executor.Registry.TryResolve(candidateName, out _)) continue;

            var remainingArgs = args.Skip(wordCount).ToList();
            var (parsedArgs, parsedOptions) = ParseArgsAndOptions(remainingArgs);

            // If config defines arg names, map positional args to them
            if (config?.Commands?.TryGetValue(candidateName, out var cmdConfig) == true &&
                cmdConfig.Args is { Count: > 0 })
            {
                var argNames = cmdConfig.Args.Keys.ToArray();
                var positionalArgs = remainingArgs
                    .Where(a => !a.StartsWith("--", StringComparison.Ordinal))
                    .ToArray();

                for (var i = 0; i < Math.Min(argNames.Length, positionalArgs.Length); i++)
                {
                    parsedArgs = new Dictionary<string, string>(parsedArgs)
                    {
                        [argNames[i]] = positionalArgs[i]
                    };
                }
            }

            var invocation = new CommandInvocation(parsedArgs, parsedOptions, json, jsonFile, workingDir);
            var result = await ExecuteCommandAsync(executor, candidateName, invocation, cancellationToken);
            return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
        }

        ConsoleRenderer.RenderError($"Command '{command}' not found. Run '{GetCliCommandName()} list' to see available commands.");
        return 8;
    }

    private static async Task<int> WriteResultAsync(
        CommandResult result,
        CommandInvocation invocation,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        if (invocation.Json)
        {
            var payload = JsonSerializer.Serialize(result, JsonOptions);
            if (!string.IsNullOrWhiteSpace(invocation.JsonFile))
            {
                var dir = Path.GetDirectoryName(invocation.JsonFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(invocation.JsonFile!, payload, cancellationToken);
            }
            else
            {
                Console.WriteLine(payload);
            }
        }
        else if (!quiet)
        {
            switch (result.Command)
            {
                case "version":
                    ConsoleRenderer.RenderVersion(result.Message ?? "unknown");
                    break;
                case "doctor":
                    ConsoleRenderer.RenderDoctorResult(result);
                    break;
                case "list":
                    ConsoleRenderer.RenderList(result);
                    break;
                case "explain":
                    ConsoleRenderer.RenderExplain(result);
                    break;
                default:
                    ConsoleRenderer.RenderCommandResult(result);
                    break;
            }

            if (verbose && result.Steps.Count > 0)
            {
                ConsoleRenderer.RenderStepResults(result.Steps);
            }
        }

        return result.ExitCode;
    }

    private static async Task<CommandResult> ExecuteCommandAsync(
        DefaultCommandExecutor executor,
        string command,
        CommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (!invocation.Json)
        {
            return await executor.ExecuteAsync(command, invocation, cancellationToken);
        }

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return await executor.ExecuteAsync(command, invocation, cancellationToken);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static async Task WriteRunManifestAsync(
        CommandResult result,
        string commandName,
        string workingDir,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string jsonFile,
        CancellationToken cancellationToken)
    {
        var manifestPath = jsonFile.Replace(".json", "-manifest.json", StringComparison.OrdinalIgnoreCase);
        if (manifestPath == jsonFile) manifestPath = jsonFile + ".manifest.json";

        // Compute SHA-256 of resolved config content for traceability
        string? configHash = null;
        var configPath = ConfigFileLocator.FindConfigPath(workingDir);
        if (configPath is not null)
        {
            var configBytes = await File.ReadAllBytesAsync(configPath, cancellationToken);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(configBytes);
            configHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var ciInfo = CiDetector.Detect();
        var gitInfo = await GitDetector.DetectAsync(workingDir, cancellationToken);

        var manifest = new RunManifest
        {
            ToolVersion = GetToolVersion(),
            RepoName = Path.GetFileName(workingDir.TrimEnd(Path.DirectorySeparatorChar)),
            RepoRoot = workingDir,
            Branch = gitInfo.Branch,
            CommitSha = gitInfo.CommitSha,
            RemoteUrl = gitInfo.RemoteUrl,
            CommandExecuted = commandName,
            Success = result.Success,
            ExitCode = result.ExitCode,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ConfigHash = configHash,
            Version = result.Version,
            AssemblyVersion = result.Version?.AssemblyVersion,
            InformationalVersion = result.Version?.InformationalVersion,
            NuGetVersion = result.Version?.NuGetVersion,
            IsCi = ciInfo.IsCi,
            CiProvider = ciInfo.Provider,
            CiBuildId = ciInfo.BuildId,
            CiRunNumber = ciInfo.RunNumber,
            CiWorkflowName = ciInfo.WorkflowName,
            CiActor = ciInfo.Actor,
            CiTag = ciInfo.Tag,
            CiBuildUrl = ciInfo.BuildUrl,
            Steps = result.Steps
                .Select(s => new StepManifestEntry(s.StepId, s.Success, s.ExitCode, s.Duration.TotalMilliseconds))
                .ToArray(),
            Artifacts = result.Artifacts,
            PushDecisions = result.PushDecisions,
            Errors = result.StructuredErrors
                .Select(e => e.Message)
                .Where(m => m is not null)
                .Select(m => m!)
                .ToArray(),
        };

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        var dir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);
    }

    private static string GetToolVersion()
    {
        var assembly = typeof(Program).Assembly;
        return assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0.1.0-local";
    }

    private static (IReadOnlyList<string> cleanArgs, bool json, string? jsonFile, bool verbose, bool debug, bool quiet, IReadOnlyList<string> setOverrides) ParseGlobalFlags(string[] args)
    {
        var clean = new List<string>();
        var json = false;
        string? jsonFile = null;
        var verbose = false;
        var debug = false;
        var quiet = false;
        var setOverrides = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--json-file" when i + 1 < args.Length:
                    jsonFile = args[++i];
                    json = true;
                    break;
                case "--verbose" or "-v":
                    verbose = true;
                    break;
                case "--debug":
                    debug = true;
                    verbose = true;
                    break;
                case "--quiet" or "-q":
                    quiet = true;
                    break;
                case "--set" when i + 1 < args.Length:
                    setOverrides.Add(args[++i]);
                    break;
                default:
                    clean.Add(args[i]);
                    break;
            }
        }

        return (clean, json, jsonFile, verbose, debug, quiet, setOverrides);
    }

    private static (IReadOnlyDictionary<string, string> args, IReadOnlyDictionary<string, string?> options)
        ParseArgsAndOptions(IEnumerable<string> args)
    {
        var parsedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parsedOptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var argList = args.ToList();

        for (var i = 0; i < argList.Count; i++)
        {
            var a = argList[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var key = a[2..];
                // Check for --key value vs --key (bool flag)
                if (i + 1 < argList.Count && !argList[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    parsedOptions[key] = argList[++i];
                }
                else
                {
                    parsedOptions[key] = "true";
                }
            }
        }

        return (parsedArgs, parsedOptions);
    }

    private static void PrintHelp()
    {
        var cli = GetCliCommandName();
        Console.WriteLine($"{cli} - repository operating system");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine($"  {cli} <command> [args] [options]");
        Console.WriteLine();
        Console.WriteLine("Built-in commands:");
        Console.WriteLine("  version              Show tool version");
        Console.WriteLine("  list                 List all available commands");
        Console.WriteLine("  explain <command>    Explain a command");
        Console.WriteLine("  doctor               Check environment and configuration");
        Console.WriteLine("  init                 Create a starter rexo config");
        Console.WriteLine("  init ci              Scaffold thin CI wrappers for rx release");
        Console.WriteLine("      --provider       github|azdo|both (default: both)");
        Console.WriteLine("      --yes            Non-interactive defaults");
        Console.WriteLine("      --template       auto|dotnet|node|python|go|generic");
        Console.WriteLine("      --schema-source  remote (default) or local");
        Console.WriteLine("      --with-policy    Also create policy.json from a template");
        Console.WriteLine("      --policy-template standard|dotnet (or any embedded template)");
        Console.WriteLine("      --with-instructions Download docs/rexo.instructions.md into repo");
        Console.WriteLine("      --instructions-path  Repo-relative destination (default: .github/instructions/rexo.instructions.md)");
        Console.WriteLine("      --force          Overwrite existing config");
        Console.WriteLine("  run <command>        Run a configured command");
        Console.WriteLine("  ui                   Open the interactive UI");
        Console.WriteLine("  help                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Global options:");
        Console.WriteLine("  --json               Output as JSON");
        Console.WriteLine("  --json-file <path>   Write JSON output to file");
        Console.WriteLine("  --verbose            Show detailed step output");
    }

    private static CommandInvocation EmptyInvocation(string workingDir, bool json, string? jsonFile) =>
        new(
            new Dictionary<string, string>(),
            new Dictionary<string, string?>(),
            Json: json,
            JsonFile: jsonFile,
            WorkingDirectory: workingDir);

    private static IReadOnlyList<string> DiscoverUiProjectRoots(string currentWorkingDir)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            currentWorkingDir,
        };

        var parent = Path.GetDirectoryName(currentWorkingDir);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        foreach (var directory in Directory.EnumerateDirectories(parent))
        {
            if (ConfigFileLocator.FindConfigPath(directory) is not null)
            {
                roots.Add(directory);
            }
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string GetCliCommandName()
    {
        var argv0 = Environment.GetCommandLineArgs().FirstOrDefault();
        var name = string.IsNullOrWhiteSpace(argv0)
            ? null
            : Path.GetFileNameWithoutExtension(argv0);

        return string.IsNullOrWhiteSpace(name) ? "rx" : name;
    }
}

