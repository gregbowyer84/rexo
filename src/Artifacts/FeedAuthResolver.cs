namespace Rexo.Artifacts;

public sealed record FeedAuthResolution(
    bool HasCredentials,
    string? Username,
    string? Secret,
    string? Endpoint,
    string? Error,
    string Source);

public static class FeedAuthResolver
{
    public static FeedAuthResolution ResolveDocker(
        string? configuredRegistry,
        string? inferredRegistry,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var username = Get("DOCKER_LOGIN_USERNAME", fileEnv) ?? Get("DOCKER_AUTH_USERNAME", fileEnv);
        var secret = Get("DOCKER_LOGIN_PASSWORD", fileEnv) ?? Get("DOCKER_AUTH_PASSWORD", fileEnv);
        var endpoint = Get("DOCKER_LOGIN_REGISTRY", fileEnv) ?? Get("DOCKER_AUTH_REGISTRY", fileEnv) ?? configuredRegistry ?? inferredRegistry;

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

    public static FeedAuthResolution ResolveHelm(
        string? configuredRegistry,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var username = Get("HELM_REGISTRY_USERNAME", fileEnv);
        var secret = Get("HELM_REGISTRY_PASSWORD", fileEnv);
        var endpoint = Get("HELM_REGISTRY", fileEnv) ?? configuredRegistry;

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
            return new FeedAuthResolution(false, null, null, endpoint, "HELM_REGISTRY_USERNAME and HELM_REGISTRY_PASSWORD must both be set.", "env");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FeedAuthResolution(false, null, null, null, "Helm login registry could not be determined. Set settings.registry or HELM_REGISTRY.", "env");
        }

        return new FeedAuthResolution(true, username, secret, endpoint, null, "env");
    }

    public static FeedAuthResolution ResolveNuGet(
        string source,
        string? configuredApiKeyEnv,
        IReadOnlyDictionary<string, string> fileEnv)
    {
        var envName = string.IsNullOrWhiteSpace(configuredApiKeyEnv) ? "NUGET_API_KEY" : configuredApiKeyEnv;
        var secret = Get(envName, fileEnv) ?? Get("NUGET_AUTH_TOKEN", fileEnv);

        if (string.IsNullOrWhiteSpace(secret))
        {
            // CI-native fallbacks for common hosted feeds.
            if (source.Contains("nuget.pkg.github.com", StringComparison.OrdinalIgnoreCase))
            {
                secret = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            }
            else
            {
                secret = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
            }

            if (!string.IsNullOrWhiteSpace(secret))
            {
                return new FeedAuthResolution(true, null, secret, source, null, "ci-token");
            }

            return new FeedAuthResolution(false, null, null, source, null, "none");
        }

        return new FeedAuthResolution(true, null, secret, source, null, "env");
    }

    private static string? Get(string key, IReadOnlyDictionary<string, string> fileEnv)
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
