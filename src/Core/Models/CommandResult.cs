namespace Rexo.Core.Models;

public sealed record CommandResult(
    string Command,
    bool Success,
    int ExitCode,
    string? Message,
    IReadOnlyDictionary<string, object?> Outputs)
{
    public IReadOnlyList<StepResult> Steps { get; init; } = Array.Empty<StepResult>();

    public static CommandResult Ok(string command, string? message = null) =>
        new(command, true, 0, message, new Dictionary<string, object?>());

    public static CommandResult Fail(string command, int exitCode, string message) =>
        new(command, false, exitCode, message, new Dictionary<string, object?>());
}
