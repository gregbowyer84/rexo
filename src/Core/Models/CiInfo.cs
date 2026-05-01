namespace Rexo.Core.Models;

public sealed record CiInfo(
    bool IsCi,
    string? Provider,
    string? BuildId,
    string? Branch,
    bool IsPullRequest);
