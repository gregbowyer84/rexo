namespace Rexo.Execution;

using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class DefaultCommandExecutor : ICommandExecutor
{
    private readonly CommandRegistry _registry;

    public DefaultCommandExecutor(CommandRegistry registry)
    {
        _registry = registry;
    }

    public CommandRegistry Registry => _registry;

    public Task<CommandResult> ExecuteAsync(
        string commandName,
        CommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (_registry.TryResolve(commandName, out var handler) && handler is not null)
        {
            return handler(invocation, cancellationToken);
        }

        return Task.FromResult(CommandResult.Fail(commandName, 8, $"Command '{commandName}' was not found."));
    }
}

