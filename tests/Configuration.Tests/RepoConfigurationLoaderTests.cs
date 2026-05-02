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
    Directory.CreateDirectory(root);
    await File.WriteAllTextAsync(
          Path.Combine(root, "rexo.schema.json"),
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
          "$schema": "rexo.schema.json",
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
      Assert.True(config.Commands!.ContainsKey("release"));
    }
    finally
    {
            File.Delete(path);
      var schemaPath = Path.Combine(root, "rexo.schema.json");
      if (File.Exists(schemaPath)) File.Delete(schemaPath);
    }
  }

  [Fact]
  public async Task LoadAsyncParsesConfigUsingEmbeddedSchemaWhenNoLocalSchemaExists()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-embedded-schema-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
        {
          "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
          "schemaVersion": "1.0",
          "name": "sample",
          "commands": {
            "build": {
              "description": "Build",
              "options": {},
              "steps": [
                { "run": "echo hello" }
              ]
            }
          },
          "aliases": {}
        }
        """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.Equal("sample", config.Name);
      Assert.True(config.Commands!.ContainsKey("build"));
    }
    finally
    {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncParsesRuntimeOutputSettings()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-output-settings-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
          {
            "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
            "schemaVersion": "1.0",
            "name": "sample",
            "commands": {
              "build": {
                "description": "Build",
                "options": {},
                "steps": [
                  { "run": "echo hello" }
                ]
              }
            },
            "aliases": {},
            "runtime": {
              "output": {
                "emitRuntimeFiles": false,
                "root": "build-output"
              }
            }
          }
          """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.NotNull(config.Runtime?.Output);
      Assert.False(config.Runtime!.Output!.EmitRuntimeFiles);
      Assert.Equal("build-output", config.Runtime.Output.Root);
    }
    finally
    {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncParsesStructuredPushRules()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-push-rules-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
          {
            "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
            "schemaVersion": "1.0",
            "name": "sample",
            "commands": {
              "build": {
                "description": "Build",
                "options": {},
                "steps": [
                  { "run": "echo hello" }
                ]
              }
            },
            "aliases": {},
            "runtime": {
              "push": {
                "enabled": true,
                "noPushInPullRequest": true,
                "requireCleanWorkingTree": true,
                "branches": ["main", "release/*"]
              }
            }
          }
          """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.NotNull(config.Runtime?.Push);
      Assert.True(config.Runtime!.Push!.Enabled);
      Assert.True(config.Runtime.Push.NoPushInPullRequest);
      Assert.True(config.Runtime.Push.RequireCleanWorkingTree);
      Assert.Equal(2, config.Runtime.Push.Branches!.Length);
    }
    finally
    {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncParsesRuntimeOutput()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-artifacts-output-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
          {
            "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
            "schemaVersion": "1.0",
            "name": "sample",
            "commands": {
              "build": {
                "description": "Build",
                "options": {},
                "steps": [
                  { "run": "echo hello" }
                ]
              }
            },
            "aliases": {},
            "runtime": {
              "output": {
                "emitRuntimeFiles": false,
                "root": "runtime-output"
              }
            }
          }
          """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.NotNull(config.Runtime?.Output);
      Assert.False(config.Runtime!.Output!.EmitRuntimeFiles);
      Assert.Equal("runtime-output", config.Runtime.Output.Root);
    }
    finally
    {
      if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
  }

    [Fact]
    public async Task LoadAsyncThrowsWhenSchemaVersionMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
        {
          "$schema": "rexo.schema.json",
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
          "$schema": "rexo.schema.json",
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

    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), minimalSchema);

    var basePath = Path.Combine(dir, "base.json");
    await File.WriteAllTextAsync(basePath, """
            {
              "$schema": "rexo.schema.json",
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
              "$schema": "rexo.schema.json",
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
      Assert.True(config.Commands!.ContainsKey("shared"), "Should inherit base command 'shared'");
      Assert.True(config.Commands!.ContainsKey("child-cmd"), "Should have own command 'child-cmd'");
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

    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), minimalSchema);

    var aPath = Path.Combine(dir, "a.json");
    var bPath = Path.Combine(dir, "b.json");

    await File.WriteAllTextAsync(aPath, $$"""
            {
              "$schema": "rexo.schema.json",
              "schemaVersion": "1.0",
              "name": "a",
              "extends": ["./b.json"],
              "commands": {},
              "aliases": {}
            }
            """);

    await File.WriteAllTextAsync(bPath, $$"""
            {
              "$schema": "rexo.schema.json",
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
    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), minimalSchema);

    var repoPath = Path.Combine(dir, "repo.json");
    await File.WriteAllTextAsync(repoPath, """
            {
              "$schema": "rexo.schema.json",
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
              "$schema": "rexo.schema.json",
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
    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), minimalSchema);

    var repoPath = Path.Combine(dir, "repo.json");
    await File.WriteAllTextAsync(repoPath, """
            {
              "$schema": "rexo.schema.json",
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
    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), minimalSchema);

    var basePath = Path.Combine(dir, "base.json");
    await File.WriteAllTextAsync(basePath, """
            {
              "$schema": "rexo.schema.json",
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
              "$schema": "rexo.schema.json",
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

      Assert.Equal("from child", config.Commands!["shared"].Description);
      Assert.True(config.Commands!.ContainsKey("base-only"), "base-only should be inherited");
      Assert.True(config.Commands!.ContainsKey("child-only"), "child-only should exist");
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncEmbeddedExtendsMergesStandardCommands()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-embedded-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
      {
        "$schema": "rexo.schema.json",
        "schemaVersion": "1.0",
        "name": "extended-sample",
        "extends": ["embedded:standard"],
        "artifacts": [
          { "type": "docker", "name": "api", "settings": { "image": "ghcr.io/acme/api" } }
        ]
      }
      """);

    // Provide a local schema that does not require commands (relaxed)
    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), """
      {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "required": ["$schema", "schemaVersion", "name"]
      }
      """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.Equal("extended-sample", config.Name);
      // Commands from embedded:standard should be inherited
      Assert.NotNull(config.Commands);
      Assert.True(config.Commands!.ContainsKey("plan"), "Should inherit 'plan' from embedded:standard");
      Assert.True(config.Commands!.ContainsKey("release"), "Should inherit 'release' from embedded:standard");
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task LoadAsyncParsesBooleanOptionDefault()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-bool-default-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
      {
        "$schema": "rexo.schema.json",
        "schemaVersion": "1.0",
        "name": "sample",
        "commands": {
          "push": {
            "description": "Push",
            "options": {
              "confirm": {
                "type": "bool",
                "default": false
              }
            },
            "steps": [
              { "uses": "builtin:push-artifacts" }
            ]
          }
        },
        "aliases": {}
      }
      """);

    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), """
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

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);
      var confirm = config.Commands!["push"].Options["confirm"].Default;
      Assert.True(confirm.HasValue);
      Assert.Equal(System.Text.Json.JsonValueKind.False, confirm.Value.ValueKind);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}

