namespace Rexo.Policies;

using System.Text.Json;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class LocalFilePolicySource : IPolicySource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<PolicyDocument> LoadAsync(string reference, CancellationToken cancellationToken)
    {
        if (!File.Exists(reference))
        {
            throw new FileNotFoundException($"Policy file not found: {reference}", reference);
        }

        var content = await File.ReadAllTextAsync(reference, cancellationToken);
        return new PolicyDocument(Source: reference, Content: content, Version: null);
    }

    public static bool CanHandle(string reference) =>
        reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        reference.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        reference.StartsWith("./", StringComparison.Ordinal) ||
        reference.StartsWith("../", StringComparison.Ordinal) ||
        Path.IsPathRooted(reference);
}
