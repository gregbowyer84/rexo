namespace Rexo.Cli;

using Rexo.Artifacts;
using Rexo.Configuration.Models;
using Rexo.Core.Models;
using Rexo.Versioning;

internal static class RuntimeParityValidator
{
    public static void ValidateOrThrow(
        RepoConfig config,
        VersionProviderRegistry versionProviders,
        ArtifactProviderRegistry artifactProviders)
    {
        ValidateVersionProvider(config, versionProviders);
        ValidateArtifactProviders(config, artifactProviders);
    }

    private static void ValidateVersionProvider(RepoConfig config, VersionProviderRegistry versionProviders)
    {
        var providerName = config.Versioning?.Provider;
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return;
        }

        if (versionProviders.IsRegistered(providerName))
        {
            return;
        }

        var supported = string.Join(", ", versionProviders.RegisteredProviders.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"[{ErrorCodes.VersionProviderUnsupported}] Unsupported versioning provider '{providerName}'. Supported providers: {supported}.");
    }

    private static void ValidateArtifactProviders(RepoConfig config, ArtifactProviderRegistry artifactProviders)
    {
        if (config.Artifacts is null || config.Artifacts.Count == 0)
        {
            return;
        }

        var unsupported = config.Artifacts
            .Select(artifact => artifact.Type)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(type => artifactProviders.Resolve(type) is null)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unsupported.Length == 0)
        {
            return;
        }

        var supported = string.Join(", ", artifactProviders.RegisteredTypes.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"[{ErrorCodes.ArtifactProviderUnsupported}] Unsupported artifact type(s): {string.Join(", ", unsupported)}. Supported artifact types: {supported}.");
    }
}
