namespace Rexo.Core.Models;

public sealed record GitInfo(
    string? Branch,
    string? CommitSha,
    string? ShortSha,
    string? RemoteUrl,
    bool IsClean);
