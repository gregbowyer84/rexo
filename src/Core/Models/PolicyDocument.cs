namespace Rexo.Core.Models;

public sealed record PolicyDocument(
    string Source,
    string Content,
    string? Version);
