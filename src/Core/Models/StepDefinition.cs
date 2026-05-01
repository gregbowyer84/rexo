namespace Rexo.Core.Models;

public sealed record StepDefinition(
    string? Id,
    string? Run,
    string? Uses,
    string? Command,
    string? When);
