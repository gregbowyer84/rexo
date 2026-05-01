namespace Rexo.Configuration.Tests;

using Rexo.Configuration;

[Collection("EnvironmentVariableSensitive")]
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
          "$schema": "schema.json",
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
          "$schema": "schema.json",
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
          "$schema": "schema.json",
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

  [Fact]
  public async Task LoadAsyncMergesExtendsConfig()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-extends-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    
    var minimalSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["$schema", "schemaVersion", "name", "commands", "aliases"],
              "properties": {
                "$schema": { "type": "string" },
                "schemaVersion": { "type": "string" },
                "name": { "type": "string" },
                "commands": { "type": "object" },
                "aliases": { "type": "object" },
                "extends": { "type": "array" }
              }
            }
            """;

    await File.WriteAllTextAsync(Path.Combine(dir, "schema.json"), minimalSchema);

    var basePath = Path.Combine(dir, "base.json");
    await File.WriteAllTextAsync(basePath, """
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "base",
              "commands": {
                "shared": { "options": {}, "steps": [] }
              },
              "aliases": {}
            }
            """);

    var childPath = Path.Combine(dir, "child.json");
    await File.WriteAllTextAsync(childPath, $$"""
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "child",
              "extends": ["./base.json"],
              "commands": {
                "child-cmd": { "options": {}, "steps": [] }
              },
              "aliases": {}
            }
            """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(childPath, CancellationToken.None);

      Assert.Equal("child", config.Name);
      Assert.True(config.Commands.ContainsKey("shared"), "Should inherit base command 'shared'");
      Assert.True(config.Commands.ContainsKey("child-cmd"), "Should have own command 'child-cmd'");
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncThrowsOnCircularExtends()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-circular-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    
    var minimalSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["$schema", "schemaVersion", "name", "commands", "aliases"],
              "properties": {
                "$schema": { "type": "string" },
                "schemaVersion": { "type": "string" },
                "name": { "type": "string" },
                "commands": { "type": "object" },
                "aliases": { "type": "object" },
                "extends": { "type": "array" }
              }
            }
            """;

    await File.WriteAllTextAsync(Path.Combine(dir, "schema.json"), minimalSchema);

    var aPath = Path.Combine(dir, "a.json");
    var bPath = Path.Combine(dir, "b.json");

    await File.WriteAllTextAsync(aPath, $$"""
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "a",
              "extends": ["./b.json"],
              "commands": {},
              "aliases": {}
            }
            """);

    await File.WriteAllTextAsync(bPath, $$"""
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "b",
              "extends": ["./a.json"],
              "commands": {},
              "aliases": {}
            }
            """);

    try
    {
      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
          RepoConfigurationLoader.LoadAsync(aPath, CancellationToken.None));

      Assert.Contains("Circular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // REXO_OVERLAY environment variable overlay tests
  // ─────────────────────────────────────────────────────────────────────────

  [Fact]
  public async Task LoadAsyncAppliesEnvironmentOverlay()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-overlay-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    
    var minimalSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["$schema", "schemaVersion", "name", "commands", "aliases"],
              "properties": {
                "$schema": { "type": "string" },
                "schemaVersion": { "type": "string" },
                "name": { "type": "string" },
                "description": { "type": "string" },
                "commands": { "type": "object" },
                "aliases": { "type": "object" }
              }
            }
            """;
    await File.WriteAllTextAsync(Path.Combine(dir, "schema.json"), minimalSchema);

    var repoPath = Path.Combine(dir, "repo.json");
    await File.WriteAllTextAsync(repoPath, """
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "base-name",
              "description": "base description",
              "commands": {},
              "aliases": {}
            }
            """);

    var overlayPath = Path.Combine(dir, "overlay.json");
    await File.WriteAllTextAsync(overlayPath, """
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "base-name",
              "description": "overlay description",
              "commands": {},
              "aliases": {}
            }
            """);

    var previous = Environment.GetEnvironmentVariable("REXO_OVERLAY");
    try
    {
      Environment.SetEnvironmentVariable("REXO_OVERLAY", overlayPath);
      var config = await RepoConfigurationLoader.LoadAsync(repoPath, CancellationToken.None);

      Assert.Equal("overlay description", config.Description);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REXO_OVERLAY", previous);
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncIgnoresOverlayWhenFileDoesNotExist()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-overlay-missing-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    
    var minimalSchema = """
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
            """;
    await File.WriteAllTextAsync(Path.Combine(dir, "schema.json"), minimalSchema);

    var repoPath = Path.Combine(dir, "repo.json");
    await File.WriteAllTextAsync(repoPath, """
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "base-repo",
              "commands": {},
              "aliases": {}
            }
            """);

    var previous = Environment.GetEnvironmentVariable("REXO_OVERLAY");
    try
    {
      Environment.SetEnvironmentVariable("REXO_OVERLAY", "/nonexistent/path/overlay.json");
      var config = await RepoConfigurationLoader.LoadAsync(repoPath, CancellationToken.None);

      Assert.Equal("base-repo", config.Name);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REXO_OVERLAY", previous);
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncMergesCommandsDictionaryChildWins()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-cmdmerge-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    
    var minimalSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["$schema", "schemaVersion", "name", "commands", "aliases"],
              "properties": {
                "$schema": { "type": "string" },
                "schemaVersion": { "type": "string" },
                "name": { "type": "string" },
                "commands": { "type": "object" },
                "aliases": { "type": "object" },
                "extends": { "type": "array" }
              }
            }
            """;
    await File.WriteAllTextAsync(Path.Combine(dir, "schema.json"), minimalSchema);

    var basePath = Path.Combine(dir, "base.json");
    await File.WriteAllTextAsync(basePath, """
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "base",
              "commands": {
                "shared": { "description": "from base", "steps": [] },
                "base-only": { "description": "base exclusive", "steps": [] }
              },
              "aliases": {}
            }
            """);

    var childPath = Path.Combine(dir, "child.json");
    await File.WriteAllTextAsync(childPath, $$"""
            {
              "$schema": "schema.json",
              "schemaVersion": "1.0",
              "name": "child",
              "extends": ["./base.json"],
              "commands": {
                "shared": { "description": "from child", "steps": [] },
                "child-only": { "description": "child exclusive", "steps": [] }
              },
              "aliases": {}
            }
            """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(childPath, CancellationToken.None);

      Assert.Equal("from child", config.Commands["shared"].Description);
      Assert.True(config.Commands.ContainsKey("base-only"), "base-only should be inherited");
      Assert.True(config.Commands.ContainsKey("child-only"), "child-only should exist");
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
