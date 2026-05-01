namespace Rexo.Configuration.Tests;

using Rexo.Configuration;

public sealed class RepoConfigurationLoaderTests
{
    [Fact]
    public async Task LoadAsyncParsesMinimalConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}.json");
        var root = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(Path.Combine(root, "schemas", "1.0"));
        await File.WriteAllTextAsync(
          Path.Combine(root, "schemas", "1.0", "schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["$schema", "schemaVersion", "name", "commands", "aliases"],
              "properties": {
                "$schema": { "type": "string" },
                "schemaVersion": { "type": "string" },
                "name": { "type": "string" },
                "commands": { "type": "object" },
                "aliases": { "type": "object" }
              }
            }
            """);

        await File.WriteAllTextAsync(path, """
        {
          "$schema": "schemas/1.0/schema.json",
          "schemaVersion": "1.0",
          "name": "sample",
          "commands": {
            "release": {
              "description": "Release",
              "options": {},
              "steps": []
            }
          },
          "aliases": {}
        }
        """);

        try
        {
            var config = await RepoConfigurationLoader.LoadAsync(path, CancellationToken.None);

            Assert.Equal("sample", config.Name);
            Assert.True(config.Commands.ContainsKey("release"));
        }
        finally
        {
            File.Delete(path);
            var schemaPath = Path.Combine(root, "schemas", "1.0", "schema.json");
            if (File.Exists(schemaPath)) File.Delete(schemaPath);
            var schemaDir = Path.Combine(root, "schemas");
            if (Directory.Exists(schemaDir)) Directory.Delete(schemaDir, true);
        }
    }

    [Fact]
    public async Task LoadAsyncThrowsWhenSchemaVersionMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
        {
          "$schema": "schemas/1.0/schema.json",
          "name": "sample",
          "commands": {
            "release": {
              "options": {},
              "steps": [
                { "run": "echo hi" }
              ]
            }
          },
          "aliases": {}
        }
        """);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RepoConfigurationLoader.LoadAsync(path, CancellationToken.None));

            Assert.Contains("schemaVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsyncThrowsWhenSchemaVersionUnsupported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
        {
          "$schema": "schemas/1.0/schema.json",
          "schemaVersion": "2.0",
          "name": "sample",
          "commands": {
            "release": {
              "options": {},
              "steps": [
                { "run": "echo hi" }
              ]
            }
          },
          "aliases": {}
        }
        """);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RepoConfigurationLoader.LoadAsync(path, CancellationToken.None));

            Assert.Contains("Unsupported schemaVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsyncThrowsWhenSchemaUriUnsupported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
        {
          "$schema": "https://example.com/repo.schema.json",
          "schemaVersion": "1.0",
          "name": "sample",
          "commands": {
            "release": {
              "options": {},
              "steps": [
                { "run": "echo hi" }
              ]
            }
          },
          "aliases": {}
        }
        """);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RepoConfigurationLoader.LoadAsync(path, CancellationToken.None));

            Assert.Contains("$schema", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
