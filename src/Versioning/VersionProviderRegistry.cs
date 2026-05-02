namespace Rexo.Versioning;

using Rexo.Core.Abstractions;

public sealed class VersionProviderRegistry
{
    private readonly Dictionary<string, IVersionProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, IVersionProvider provider) =>
        _providers[name] = provider;

    public IVersionProvider Resolve(string name) =>
        _providers.TryGetValue(name, out var p) ? p : new FixedVersionProvider();

    public static VersionProviderRegistry CreateDefault()
    {
        var registry = new VersionProviderRegistry();
        registry.Register("auto", new AutoVersionProvider());
        registry.Register("fixed", new FixedVersionProvider());
        registry.Register("env", new EnvVersionProvider());
        registry.Register("git", new GitTagVersionProvider());
        registry.Register("gitversion", new GitVersionVersionProvider());
        registry.Register("nbgv", new NbgvVersionProvider());
        registry.Register("minver", new MinVerVersionProvider());
        return registry;
    }
}
