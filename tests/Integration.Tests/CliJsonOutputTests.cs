namespace Rexo.Integration.Tests;

using System.Globalization;
using System.Text.Json;
using Rexo.Cli;

[Collection("IntegrationSequential")]
public sealed class CliJsonOutputTests
{
    [Fact]
    public async Task JsonModeWritesOnlyJsonToStdout()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rexo-cli-json-only-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var originalDirectory = Environment.CurrentDirectory;
        var originalOut = Console.Out;
        var originalError = Console.Error;

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
                    "hello": {
                      "description": "Test command",
                      "steps": [
                        { "id": "resolve", "uses": "builtin:resolve-version" }
                      ]
                    }
                  },
                  "versioning": {
                    "provider": "fixed",
                    "fallback": "1.2.3"
                  }
                }
                """);

            Environment.CurrentDirectory = tempDir;

            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await Program.ExecuteAsync(["--json", "hello"], CancellationToken.None);

            Assert.Equal(0, exitCode);

            var output = stdout.ToString().Trim();
            Assert.NotEmpty(output);
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal("hello", parsed.RootElement.GetProperty("Command").GetString());
            Assert.DoesNotContain("Resolved version", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("  > ", output, StringComparison.Ordinal);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.CurrentDirectory = originalDirectory;

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
