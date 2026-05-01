namespace Rexo.Core.Models;

public sealed record CommandResult(
    string Command,
    bool Success,
    int ExitCode,
    string? Message,
    IReadOnlyDictionary<string, object?> Outputs)
{
    public IReadOnlyList<StepResult> Steps { get; init; } = Array.Empty<StepResult>();

    /// <summary>Resolved version, if <c>builtin:resolve-version</c> ran during this command.</summary>
    public VersionResult? Version { get; init; }

    /// <summary>Structured errors with machine-readable codes.</summary>
    public IReadOnlyList<RexoError> StructuredErrors { get; init; } = Array.Empty<RexoError>();

    public static CommandResult Ok(string command, string? message = null) =>
        new(command, true, 0, message, new Dictionary<string, object?>());

    public static CommandResult Fail(string command, int exitCode, string message) =>
        new(command, false, exitCode, message, new Dictionary<string, object?>());

    public static CommandResult FailWithError(string command, int exitCode, RexoError error) =>
        new(command, false, exitCode, error.Message, new Dictionary<string, object?>())
        { StructuredErrors = [error] };
}
