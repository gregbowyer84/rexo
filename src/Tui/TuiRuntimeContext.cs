namespace Rexo.Tui;

using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Execution;

internal static class TuiRuntimeContext
{
    public sealed record RunRecord(
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        string Command,
        bool Success,
        int ExitCode,
        string? Message,
        IReadOnlyDictionary<string, string> Args,
        IReadOnlyDictionary<string, string?> Options,
        int StepCount,
        TimeSpan Duration);

    private static readonly List<RunRecord> Runs = [];

    public static DefaultCommandExecutor Executor { get; private set; } = default!;
    public static RepoConfig? Config { get; private set; }
    public static string WorkingDirectory { get; private set; } = string.Empty;
    public static IReadOnlyList<RunRecord> RecentRuns => Runs;

    public static void Initialize(DefaultCommandExecutor executor, RepoConfig? config, string workingDirectory)
    {
        Executor = executor;
        Config = config;
        WorkingDirectory = workingDirectory;
        Runs.Clear();
    }

    public static void AddRun(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string command,
        IReadOnlyDictionary<string, string> args,
        IReadOnlyDictionary<string, string?> options,
        CommandResult result)
    {
        Runs.Insert(0, new RunRecord(
            StartedAt: startedAt,
            CompletedAt: completedAt,
            Command: command,
            Success: result.Success,
            ExitCode: result.ExitCode,
            Message: result.Message,
            Args: new Dictionary<string, string>(args, StringComparer.OrdinalIgnoreCase),
            Options: new Dictionary<string, string?>(options, StringComparer.OrdinalIgnoreCase),
            StepCount: result.Steps.Count,
            Duration: completedAt - startedAt));

        if (Runs.Count > 100)
        {
            Runs.RemoveRange(100, Runs.Count - 100);
        }
    }
}
