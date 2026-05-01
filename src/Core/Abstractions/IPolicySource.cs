namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface IPolicySource
{
    Task<PolicyDocument> LoadAsync(
        string reference,
        CancellationToken cancellationToken);
}
