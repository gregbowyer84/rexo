namespace Rexo.Configuration;

public static class ConfigFileLocator
{
    private static readonly string[] ConfigRelativeCandidates =
    [
        "rexo.json",
        "rexo.yaml",
        "rexo.yml",
        ".rexo/rexo.json",
        ".rexo/rexo.yaml",
        ".rexo/rexo.yml",
        "repo.json",
        "repo.yaml",
        "repo.yml",
        ".repo/repo.json",
        ".repo/repo.yaml",
        ".repo/repo.yml",
    ];

    private static readonly string[] PolicyRelativeCandidates =
    [
        "policy.json",
        "policy.yaml",
        "policy.yml",
        ".rexo/policy.json",
        ".rexo/policy.yaml",
        ".rexo/policy.yml",
        ".repo/policy.json",
        ".repo/policy.yaml",
        ".repo/policy.yml",
    ];

    public static IReadOnlyList<string> GetConfigCandidates(string workingDirectory) =>
        ConfigRelativeCandidates
            .Select(relative => Path.Combine(workingDirectory, NormalizeRelativePath(relative)))
            .ToArray();

    public static IReadOnlyList<string> GetPolicyCandidates(string workingDirectory) =>
        PolicyRelativeCandidates
            .Select(relative => Path.Combine(workingDirectory, NormalizeRelativePath(relative)))
            .ToArray();

    public static string? FindConfigPath(string workingDirectory) =>
        GetConfigCandidates(workingDirectory).FirstOrDefault(File.Exists);

    public static string? FindPolicyPath(string workingDirectory) =>
        GetPolicyCandidates(workingDirectory).FirstOrDefault(File.Exists);

    public static string GetDefaultConfigPath(string workingDirectory) =>
        Path.Combine(workingDirectory, "rexo.json");

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar);
}
