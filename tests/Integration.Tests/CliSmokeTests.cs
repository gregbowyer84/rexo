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
    public async Task EmbeddedStandardCommandsAvailableWithoutPolicyFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rexo-cli-embedded-standard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var originalDirectory = Environment.CurrentDirectory;

        try
        {
            // Truly artifacts-only config: no commands or aliases section
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "rexo.json"),
                """
                {
                  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
                  "schemaVersion": "1.0",
                  "name": "sample",
                  "artifacts": [
                    {
                      "type": "docker",
                      "name": "api",
                      "settings": {
                        "image": "ghcr.io/acme/api"
                      }
                    }
                  ]
                }
                """);

            Environment.CurrentDirectory = tempDir;

            var listJsonPath = Path.Combine(tempDir, "list-embedded.json");
            var listExitCode = await Program.ExecuteAsync(["--json-file", listJsonPath, "--json", "list"], CancellationToken.None);
            Assert.Equal(0, listExitCode);

            var listOutput = await File.ReadAllTextAsync(listJsonPath);
            Assert.Contains("plan", listOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("release", listOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("publish", listOutput, StringComparison.OrdinalIgnoreCase);

            var explainExitCode = await Program.ExecuteAsync(["explain", "release"], CancellationToken.None);
            Assert.Equal(0, explainExitCode);

            var planExitCode = await Program.ExecuteAsync(["plan"], CancellationToken.None);
            Assert.Equal(0, planExitCode);
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

    [Fact]
    public async Task ShipWithoutConfirmSkipsPushAndSucceeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rexo-cli-ship-skip-{Guid.NewGuid():N}");
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
                                    "artifacts": [
                                        {
                                            "type": "docker",
                                            "name": "api",
                                            "settings": {
                                                "image": "ghcr.io/acme/api"
                                            }
                                        }
                                    ]
                                }
                                """);

            Environment.CurrentDirectory = tempDir;
            var shipExitCode = await Program.ExecuteAsync(["ship"], CancellationToken.None);
            Assert.Equal(0, shipExitCode);
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

        [Fact]
        public async Task RemotePolicySourceFromEnvironmentIsMerged()
        {
                var tempDir = Path.Combine(Path.GetTempPath(), $"rexo-cli-remote-policy-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                var originalDirectory = Environment.CurrentDirectory;
                var originalSources = Environment.GetEnvironmentVariable("REXO_POLICY_SOURCES");

                try
                {
                        await File.WriteAllTextAsync(
                                Path.Combine(tempDir, "rexo.json"),
                                """
                                {
                                    "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
                                    "schemaVersion": "1.0",
                                    "name": "sample",
                                    "versioning": {
                                        "provider": "fixed",
                                        "fallback": "1.2.3"
                                    }
                                }
                                """);

                        var externalPolicy = Path.Combine(tempDir, "external.policy.json");
                        await File.WriteAllTextAsync(
                                externalPolicy,
                                """
                                {
                                    "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json",
                                    "schemaVersion": "1.0",
                                    "name": "external-policy",
                                    "commands": {
                                        "from remote": {
                                            "description": "Resolve version from environment policy source",
                                            "steps": [
                                                { "uses": "builtin:resolve-version" }
                                            ]
                                        }
                                    },
                                    "aliases": {}
                                }
                                """);

                        Environment.SetEnvironmentVariable("REXO_POLICY_SOURCES", externalPolicy);
                        Environment.CurrentDirectory = tempDir;

                        var listJsonPath = Path.Combine(tempDir, "list-remote-policy.json");
                        var listExitCode = await Program.ExecuteAsync(["--json-file", listJsonPath, "--json", "list"], CancellationToken.None);
                        Assert.Equal(0, listExitCode);

                        var listOutput = await File.ReadAllTextAsync(listJsonPath);
                        Assert.Contains("from remote", listOutput, StringComparison.OrdinalIgnoreCase);

                        var commandExitCode = await Program.ExecuteAsync(["from", "remote"], CancellationToken.None);
                        Assert.Equal(0, commandExitCode);
                }
                finally
                {
                        Environment.SetEnvironmentVariable("REXO_POLICY_SOURCES", originalSources);
                        Environment.CurrentDirectory = originalDirectory;

                        if (Directory.Exists(tempDir))
                        {
                                Directory.Delete(tempDir, true);
                        }
                }
        }
}
