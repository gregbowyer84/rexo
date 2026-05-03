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
    public static FeedAuthResolution ResolveDocker(
        string? configuredRegistry,
        string? inferredRegistry,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var username = GetEnv("DOCKER_LOGIN_USERNAME", fileEnv) ?? GetEnv("DOCKER_AUTH_USERNAME", fileEnv);
        var secret = GetEnv("DOCKER_LOGIN_PASSWORD", fileEnv) ?? GetEnv("DOCKER_AUTH_PASSWORD", fileEnv);
        var endpoint = GetEnv("DOCKER_LOGIN_REGISTRY", fileEnv) ?? GetEnv("DOCKER_AUTH_REGISTRY", fileEnv) ?? configuredRegistry ?? inferredRegistry;

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
