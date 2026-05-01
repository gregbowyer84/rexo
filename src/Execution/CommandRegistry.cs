namespace Rexo.Execution;

using Rexo.Core.Models;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, Func<CommandInvocation, CancellationToken, Task<CommandResult>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Names => _handlers.Keys.ToArray();

    public void Register(string name, Func<CommandInvocation, CancellationToken, Task<CommandResult>> handler)
    {
        _handlers[name] = handler;
    }

    public bool TryResolve(string name, out Func<CommandInvocation, CancellationToken, Task<CommandResult>>? handler) =>
        _handlers.TryGetValue(name, out handler);
}
