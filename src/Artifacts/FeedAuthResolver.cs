namespace Rexo.Artifacts;

/// <summary>
/// Resolved credentials from env vars, file env, or CI-native token sources.
/// </summary>
public sealed record FeedAuthResolution(
    bool HasCredentials,
    string? Username,
    string? Secret,
    string? Endpoint,
    string? Error,
    string Source);

/// <summary>
/// Shared auth infrastructure.  Only cross-provider concerns live here:
/// Docker login (used by both the Docker and DockerCompose providers) and
/// the <see cref="GetEnv"/> utility that providers use in their own resolver logic.
/// Provider-specific auth knowledge lives inside each provider.
/// </summary>
public static class FeedAuthResolver
{
    /// <summary>
    /// Resolves a target value using environment and settings indirection.
    /// Order: configured env-name (or default env-name) -> configured value.
    /// </summary>
    public static string? ResolveTargetValue(
        string defaultEnvName,
        string? configuredEnvName,
        string? configuredValue,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var envName = string.IsNullOrWhiteSpace(configuredEnvName)
            ? defaultEnvName
            : configuredEnvName;

        return GetEnv(envName, fileEnv) ?? configuredValue;
    }

    /// <summary>
    /// Resolves a secret/token value using environment and optional fallback aliases.
    /// Order: configured env-name (or default env-name) -> fallback env names.
    /// </summary>
    public static string? ResolveSecret(
        string defaultEnvName,
        string? configuredEnvName,
        IReadOnlyDictionary<string, string> fileEnv,
        params string[] fallbackEnvNames)
    {
        var envName = string.IsNullOrWhiteSpace(configuredEnvName)
            ? defaultEnvName
            : configuredEnvName;

        var resolved = GetEnv(envName, fileEnv);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        foreach (var fallbackEnvName in fallbackEnvNames)
        {
            resolved = GetEnv(fallbackEnvName, fileEnv);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    public static FeedAuthResolution ResolveDocker(
        string? configuredRegistry,
        string? inferredRegistry,
        IReadOnlyDictionary<string, string> fileEnv,
        string? configuredUsernameEnv = null,
        string? configuredPasswordEnv = null,
        string? configuredRegistryEnv = null)
    {
        var username = ResolveSecret(
            defaultEnvName: "DOCKER_LOGIN_USERNAME",
            configuredEnvName: configuredUsernameEnv,
            fileEnv: fileEnv,
            "DOCKER_AUTH_USERNAME");
        var secret = ResolveSecret(
            defaultEnvName: "DOCKER_LOGIN_PASSWORD",
            configuredEnvName: configuredPasswordEnv,
            fileEnv: fileEnv,
            "DOCKER_AUTH_PASSWORD");
        var endpoint = ResolveTargetValue(
                           defaultEnvName: "DOCKER_LOGIN_REGISTRY",
                           configuredEnvName: configuredRegistryEnv,
                           configuredValue: configuredRegistry,
                           fileEnv: fileEnv)
                       ?? GetEnv("DOCKER_AUTH_REGISTRY", fileEnv)
                       ?? inferredRegistry;

        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(secret))
        {
            // CI-native fallback for GHCR.
            if (!string.IsNullOrWhiteSpace(endpoint) && endpoint.Contains("ghcr.io", StringComparison.OrdinalIgnoreCase))
            {
                var actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
                var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (!string.IsNullOrWhiteSpace(actor) && !string.IsNullOrWhiteSpace(token))
                {
                    return new FeedAuthResolution(true, actor, token, endpoint, null, "github-token");
                }
            }

            return new FeedAuthResolution(false, null, null, endpoint, null, "none");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(secret))
        {
            return new FeedAuthResolution(false, null, null, endpoint, "DOCKER_LOGIN_USERNAME and DOCKER_LOGIN_PASSWORD must both be set.", "env");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FeedAuthResolution(false, null, null, null, "Docker login registry could not be determined. Set settings.loginRegistry or DOCKER_LOGIN_REGISTRY.", "env");
        }

        return new FeedAuthResolution(true, username, secret, endpoint, null, "env");
    }

    /// <summary>
    /// Reads <paramref name="key"/> from the process environment first, then falls back to
    /// <paramref name="fileEnv"/> (loaded from .env / repo env files).  Returns <c>null</c>
    /// when the key is absent or blank in both sources.
    /// </summary>
    public static string? GetEnv(string key, IReadOnlyDictionary<string, string> fileEnv)
    {
        var process = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(process))
        {
            return process;
        }

        return fileEnv.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue)
            ? fileValue
            : null;
    }
}
