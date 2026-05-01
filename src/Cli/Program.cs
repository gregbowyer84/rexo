namespace Rexo.Cli;

using System.Text.Json;
using Rexo.Artifacts;
using Rexo.Artifacts.Docker;
using Rexo.Artifacts.NuGet;
using Rexo.Configuration;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Execution;
using Rexo.Templating;
using Rexo.Ui;
using Rexo.Versioning;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task<int> Main(string[] args) => ExecuteAsync(args, CancellationToken.None);

    public static async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var workingDir = Environment.CurrentDirectory;

        // Parse global flags
        var (cleanArgs, json, jsonFile, verbose, debug, quiet) = ParseGlobalFlags(args);

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
        var (registry, executor, config) = await BuildServicesAsync(workingDir, debug, cancellationToken);

        return command switch
        {
            "version" => await RunBuiltinAsync(executor, "version", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "doctor" => await RunBuiltinAsync(executor, "doctor", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "list" => await RunBuiltinAsync(executor, "list", EmptyInvocation(workingDir, json, jsonFile), verbose, quiet, cancellationToken),
            "explain" => await RunExplainAsync(executor, cleanArgs, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            "config" => await RunConfigSubcommandAsync(cleanArgs, executor, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            "ui" => await RunUiAsync(executor, workingDir, cancellationToken),
            "run" => await RunConfiguredAsync(cleanArgs, executor, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
            _ => await RunDirectAsync(command, cleanArgs, executor, config, workingDir, json, jsonFile, verbose, quiet, cancellationToken),
        };
    }

    private static async Task<(CommandRegistry registry, DefaultCommandExecutor executor, RepoConfig? config)>
        BuildServicesAsync(string workingDir, bool debug, CancellationToken cancellationToken)
    {
        if (debug) Console.WriteLine($"[debug] Loading configuration from {workingDir}");
        // Try load config
        RepoConfig? config = null;
        var configPath = Path.Combine(workingDir, "repo.json");
        if (File.Exists(configPath))
        {
            try
            {
                config = await RepoConfigurationLoader.LoadAsync(configPath, cancellationToken);
                if (debug) Console.WriteLine($"[debug] Loaded repo.json: {config.Name}");
            }
            catch (Exception ex)
            {
                ConsoleRenderer.RenderError($"Failed to load repo.json: {ex.Message}");
            }
        }

        // Build registry
        var registry = BuiltinCommandRegistration.CreateDefault(config, File.Exists(configPath) ? configPath : null);
        var executor = new DefaultCommandExecutor(registry);

        if (config is not null)
        {
            // Set up provider registries
            var templateRenderer = new TemplateRenderer();
            var versionProviders = VersionProviderRegistry.CreateDefault();
            var artifactProviders = new ArtifactProviderRegistry();
            artifactProviders.Register("docker", new DockerArtifactProvider());
            artifactProviders.Register("nuget", new NuGetArtifactProvider());

            var builtinRegistry = new BuiltinRegistry();
            var configLoader = new ConfigCommandLoader(
                builtinRegistry,
                templateRenderer,
                versionProviders,
                artifactProviders);

            configLoader.LoadInto(registry, config, workingDir, executor);
        }

        return (registry, executor, config);
    }

    private static async Task<int> RunBuiltinAsync(
        DefaultCommandExecutor executor,
        string command,
        CommandInvocation invocation,
        bool verbose,
        bool quiet,
        CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(command, invocation, cancellationToken);
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

        var result = await executor.ExecuteAsync("explain", invocation, cancellationToken);
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
            Console.WriteLine("Usage: rx config <resolved|sources>");
            return 1;
        }

        var subCommand = $"config {args[1].ToLowerInvariant()}";
        var invocation = EmptyInvocation(workingDir, json, jsonFile);
        var result = await executor.ExecuteAsync(subCommand, invocation, cancellationToken);
        return await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);
    }

    private static async Task<int> RunUiAsync(
        DefaultCommandExecutor executor,
        string workingDir,
        CancellationToken cancellationToken)
    {
        // List all available commands for a basic command picker UI
        var listResult = await executor.ExecuteAsync("list", EmptyInvocation(workingDir, false, null), cancellationToken);
        ConsoleRenderer.RenderList(listResult);
        return 0;
    }

    private static async Task<int> RunConfiguredAsync(
        IReadOnlyList<string> args,
        DefaultCommandExecutor executor,
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

        // Collect multi-word command: run branch feature [name]
        // Everything after "run" up to the first option is the command name
        var commandParts = new List<string>();
        var remainingArgs = new List<string>();
        var collectingCommand = true;

        for (var i = 1; i < args.Count; i++)
        {
            if (collectingCommand && !args[i].StartsWith("--", StringComparison.Ordinal))
            {
                commandParts.Add(args[i]);
            }
            else
            {
                collectingCommand = false;
                remainingArgs.Add(args[i]);
            }
        }

        var commandName = string.Join(" ", commandParts);
        var (parsedArgs, parsedOptions) = ParseArgsAndOptions(remainingArgs);
        var invocation = new CommandInvocation(parsedArgs, parsedOptions, json, jsonFile, workingDir);

        var startedAt = DateTimeOffset.UtcNow;
        var result = await executor.ExecuteAsync(commandName, invocation, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;

        var exitCode = await WriteResultAsync(result, invocation, verbose, quiet, cancellationToken);

        // Write run manifest if --json-file specified
        if (!string.IsNullOrEmpty(jsonFile))
        {
            await WriteRunManifestAsync(result, commandName, workingDir, startedAt, completedAt, jsonFile, cancellationToken);
        }

        return exitCode;
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
            if (config?.Commands.TryGetValue(candidateName, out var cmdConfig) == true &&
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
            var result = await executor.ExecuteAsync(candidateName, invocation, cancellationToken);
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
                if (!quiet) Console.WriteLine($"JSON output written to {invocation.JsonFile}");
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

        var manifest = new RunManifest
        {
            ToolVersion = GetToolVersion(),
            RepoRoot = workingDir,
            CommandExecuted = commandName,
            Success = result.Success,
            ExitCode = result.ExitCode,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Steps = result.Steps
                .Select(s => new StepManifestEntry(s.StepId, s.Success, s.ExitCode, s.Duration.TotalMilliseconds))
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

    private static (IReadOnlyList<string> cleanArgs, bool json, string? jsonFile, bool verbose, bool debug, bool quiet) ParseGlobalFlags(string[] args)
    {
        var clean = new List<string>();
        var json = false;
        string? jsonFile = null;
        var verbose = false;
        var debug = false;
        var quiet = false;

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
                default:
                    clean.Add(args[i]);
                    break;
            }
        }

        return (clean, json, jsonFile, verbose, debug, quiet);
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

    private static string GetCliCommandName()
    {
        var argv0 = Environment.GetCommandLineArgs().FirstOrDefault();
        var name = string.IsNullOrWhiteSpace(argv0)
            ? null
            : Path.GetFileNameWithoutExtension(argv0);

        return string.IsNullOrWhiteSpace(name) ? "rx" : name;
    }
}

