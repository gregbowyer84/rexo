namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync(
        string commandName,
        CommandInvocation invocation,
        CancellationToken cancellationToken);
}
