namespace Rexo.Core.Models;

public sealed record StepDefinition(
    string? Id,
    string? Run,
    string? Uses,
    string? Command,
    string? When)
{
    public IReadOnlyDictionary<string, string>? With { get; init; }
    public bool Parallel { get; init; }
    public bool ContinueOnError { get; init; }
    public string? OutputPattern { get; init; }
    public string? OutputFile { get; init; }
}
