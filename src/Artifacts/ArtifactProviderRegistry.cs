namespace Rexo.Artifacts;

using Rexo.Core.Abstractions;

public sealed class ArtifactProviderRegistry
{
    private readonly Dictionary<string, IArtifactProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string type, IArtifactProvider provider) =>
        _providers[type] = provider;

    public IArtifactProvider? Resolve(string type) =>
        _providers.TryGetValue(type, out var p) ? p : null;

    public IReadOnlyCollection<string> RegisteredTypes => _providers.Keys.ToArray();
}
