namespace Rexo.Execution;

using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class DefaultCommandExecutor : ICommandExecutor
{
    private readonly CommandRegistry _registry;

    public DefaultCommandExecutor(CommandRegistry registry)
    {
        _registry = registry;
    }

    public CommandRegistry Registry => _registry;

    public Task<CommandResult> ExecuteAsync(
        string commandName,
        CommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (_registry.TryResolve(commandName, out var handler) && handler is not null)
        {
            return handler(invocation, cancellationToken);
        }

        // Suggest similar commands using Levenshtein distance
        var allCommands = _registry.Names;
        var suggestions = allCommands
            .Select(n => (Name: n, Distance: LevenshteinDistance(commandName, n)))
            .Where(t => t.Distance <= 3)
            .OrderBy(t => t.Distance)
            .Take(3)
            .Select(t => t.Name)
            .ToList();

        var message = suggestions.Count > 0
            ? $"Command '{commandName}' was not found. Did you mean: {string.Join(", ", suggestions)}?"
            : $"Command '{commandName}' was not found. Run 'rx list' to see available commands.";

        var suggestedFix = suggestions.Count > 0
            ? $"Did you mean: {string.Join(" | ", suggestions)}? Run 'rx list' for all commands."
            : "Run 'rx list' to see all available commands.";

        return Task.FromResult(CommandResult.FailWithError(
            commandName,
            8,
            new RexoError(ErrorCodes.CommandNotFound, message)
            {
                SuggestedFix = suggestedFix,
                Detail = suggestions.Count > 0
                    ? $"Closest matches (Levenshtein distance ≤ 3): {string.Join(", ", suggestions)}"
                    : null,
            }));
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }
}

