namespace Rexo.Core.Models;

public sealed record VersioningConfig(
    string Provider,
    string? Fallback = null,
    IReadOnlyDictionary<string, string>? Settings = null);
