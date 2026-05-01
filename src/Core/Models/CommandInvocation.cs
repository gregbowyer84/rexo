namespace Rexo.Core.Models;

public sealed record CommandInvocation(
    IReadOnlyDictionary<string, string> Args,
    IReadOnlyDictionary<string, string?> Options,
    bool Json,
    string? JsonFile,
    string WorkingDirectory);
