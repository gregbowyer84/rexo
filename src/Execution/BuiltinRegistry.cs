namespace Rexo.Execution;

using Rexo.Core.Models;

public sealed class BuiltinRegistry
{
    private readonly Dictionary<string, Func<StepDefinition, ExecutionContext, CancellationToken, Task<StepResult>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(
        string name,
        Func<StepDefinition, ExecutionContext, CancellationToken, Task<StepResult>> handler) =>
        _handlers[name] = handler;

    public bool TryResolve(
        string name,
        out Func<StepDefinition, ExecutionContext, CancellationToken, Task<StepResult>>? handler) =>
        _handlers.TryGetValue(name, out handler);
}
