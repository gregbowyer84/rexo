namespace Rexo.Configuration.Tests;

using Rexo.Cli;

[Collection("EnvironmentVariableSensitive")]
public sealed class CliBootstrapperParityTests
{
    [Fact]
    public async Task BuildServicesAsyncThrowsWhenArtifactTypeIsUnsupported()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-parity-artifact-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var configPath = Path.Combine(dir, "rexo.json");
        await File.WriteAllTextAsync(configPath, """
        {
          "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json",
          "schemaVersion": "1.0",
          "name": "sample",
          "commands": {},
          "aliases": {},
          "artifacts": [
            {
              "type": "not-a-real-provider",
              "name": "bad"
            }
          ]
        }
        """);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CliBootstrapper.BuildServicesAsync(dir, debug: false, setOverrides: null, CancellationToken.None));

            Assert.Contains("ART-005", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not-a-real-provider", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

}

