namespace Rexo.Core.Models;

public sealed record StepResult(
    string StepId,
    bool Success,
    int ExitCode,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object?> Outputs);
