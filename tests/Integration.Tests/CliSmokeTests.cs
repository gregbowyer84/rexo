namespace Rexo.Integration.Tests;

using Rexo.Cli;

public sealed class CliSmokeTests
{
    [Fact]
    public async Task VersionCommandReturnsSuccess()
    {
        var exitCode = await Program.ExecuteAsync(["version"], CancellationToken.None);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task PolicyCommandAppearsInListAndExecutesDirectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rexo-cli-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var originalDirectory = Environment.CurrentDirectory;

        try
        {
            await File.WriteAllTextAsync(
                    Path.Combine(tempDir, "rexo.json"),
                    """
                                {
                                    "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
                                    "schemaVersion": "1.0",
                                    "name": "sample",
                                    "commands": {
                                        "local": {
                                            "description": "Local command",
                                            "steps": [
                                                { "id": "local-step", "uses": "builtin:resolve-version" }
                                            ]
                                        }
                                    },
                                    "aliases": {},
                                    "versioning": {
                                        "provider": "fixed",
                                        "fallback": "1.2.3"
                                    }
                                }
                                """);

            await File.WriteAllTextAsync(
                    Path.Combine(tempDir, "policy.json"),
                    """
                                {
                                    "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json",
                                    "schemaVersion": "1.0",
                                    "name": "sample-policy",
                                    "commands": {
                                        "release": {
                                            "description": "Resolve version from policy",
                                            "steps": [
                                                { "uses": "builtin:resolve-version" }
                                            ]
                                        }
                                    },
                                    "aliases": {}
                                }
                                """);

            Environment.CurrentDirectory = tempDir;

            var listJsonPath = Path.Combine(tempDir, "list.json");
            var listExitCode = await Program.ExecuteAsync(["--json-file", listJsonPath, "--json", "list"], CancellationToken.None);
            Assert.Equal(0, listExitCode);

            var listOutput = await File.ReadAllTextAsync(listJsonPath);
            Assert.Contains("release", listOutput, StringComparison.OrdinalIgnoreCase);

            var releaseExitCode = await Program.ExecuteAsync(["release"], CancellationToken.None);
            Assert.Equal(0, releaseExitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
