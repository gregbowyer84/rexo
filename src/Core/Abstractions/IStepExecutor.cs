namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface IStepExecutor
{
    Task<StepResult> ExecuteAsync(
    StepDefinition stepDefinition,
        ExecutionContext context,
        CancellationToken cancellationToken);
}
