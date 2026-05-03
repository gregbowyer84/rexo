namespace Rexo.Configuration.Tests;

using System.Text.Json;
using Rexo.Configuration;
using Rexo.Policies;

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
  public async Task LoadAsyncArtifactOnlyConfigDoesNotApplyStandardTemplateImplicitly()
  {
    var dir = Path.Combine(Path.GetTempPath(), $"rexo-implicit-standard-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);

    var configPath = Path.Combine(dir, "rexo.json");
    await File.WriteAllTextAsync(configPath, """
      {
        "$schema": "rexo.schema.json",
        "schemaVersion": "1.0",
        "name": "artifact-only",
        "artifacts": [
          { "type": "docker", "name": "api", "settings": { "image": "ghcr.io/acme/api" } }
        ]
      }
      """);

    await File.WriteAllTextAsync(Path.Combine(dir, "rexo.schema.json"), """
      {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "required": ["$schema", "schemaVersion", "name"],
        "properties": {
          "$schema": { "type": "string" },
          "schemaVersion": { "type": "string" },
          "name": { "type": "string" },
          "artifacts": { "type": "array" }
        }
      }
      """);

    try
    {
      var config = await RepoConfigurationLoader.LoadAsync(configPath, CancellationToken.None);

      Assert.True(config.Commands is null || config.Commands.Count == 0);
      Assert.True(config.Aliases is null || config.Aliases.Count == 0);
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

  [Fact]
  public void EmbeddedDotnetTemplateBooleanDefaultsDeserializeAsBooleans()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet"));
    var commands = document.RootElement.GetProperty("commands");

    var releasePushDefault = commands
      .GetProperty("release")
      .GetProperty("options")
      .GetProperty("push")
      .GetProperty("default");

    var formatFixDefault = commands
      .GetProperty("format")
      .GetProperty("options")
      .GetProperty("fix")
      .GetProperty("default");

    Assert.Equal(JsonValueKind.False, releasePushDefault.ValueKind);
    Assert.Equal(JsonValueKind.False, formatFixDefault.ValueKind);
  }

  [Fact]
  public void EmbeddedDotnetTemplateKeepsOnlyShortConvenienceAliases()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet"));
    var aliases = document.RootElement.GetProperty("aliases");

    Assert.True(aliases.TryGetProperty("r", out _));
    Assert.True(aliases.TryGetProperty("f", out _));
    Assert.False(aliases.TryGetProperty("build", out _));
    Assert.False(aliases.TryGetProperty("publish", out _));
  }

  [Fact]
  public void EmbeddedTemplateNamesIncludesDotnetLibraryAndDotnetApi()
  {
    var names = EmbeddedPolicyTemplates.TemplateNames;
    Assert.Contains("dotnet-library", names);
    Assert.Contains("dotnet-api", names);
  }

  [Fact]
  public void EmbeddedDotnetLibraryTemplateHasExpectedCommandsAndAliases()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet-library"));
    var root = document.RootElement;
    var commands = root.GetProperty("commands");

    Assert.True(commands.TryGetProperty("ci", out _), "Should have 'ci'");
    Assert.True(commands.TryGetProperty("release", out _), "Should have 'release'");
    Assert.True(commands.TryGetProperty("restore", out _), "Should have 'restore'");
    Assert.True(commands.TryGetProperty("format", out _), "Should have 'format'");
    Assert.True(commands.TryGetProperty("pack", out _), "Should have 'pack'");
    Assert.False(commands.TryGetProperty("stage", out _), "Should not have 'stage'");

    var aliases = root.GetProperty("aliases");
    Assert.True(aliases.TryGetProperty("r", out _), "Should have alias 'r'");
    Assert.True(aliases.TryGetProperty("f", out _), "Should have alias 'f'");
    Assert.True(aliases.TryGetProperty("p", out _), "Should have alias 'p'");
  }

  [Fact]
  public void EmbeddedDotnetLibraryTemplateBooleanDefaultsDeserializeAsBooleans()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet-library"));
    var commands = document.RootElement.GetProperty("commands");

    var releasePushDefault = commands
      .GetProperty("release")
      .GetProperty("options")
      .GetProperty("push")
      .GetProperty("default");

    Assert.Equal(JsonValueKind.False, releasePushDefault.ValueKind);
  }

  [Fact]
  public void EmbeddedDotnetApiTemplateHasExpectedCommandsAndAliases()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet-api"));
    var root = document.RootElement;
    var commands = root.GetProperty("commands");

    Assert.True(commands.TryGetProperty("ci", out _), "Should have 'ci'");
    Assert.True(commands.TryGetProperty("release", out _), "Should have 'release'");
    Assert.True(commands.TryGetProperty("restore", out _), "Should have 'restore'");
    Assert.True(commands.TryGetProperty("format", out _), "Should have 'format'");
    Assert.True(commands.TryGetProperty("stage", out _), "Should have 'stage'");
    Assert.False(commands.TryGetProperty("pack", out _), "Should not have 'pack'");

    var aliases = root.GetProperty("aliases");
    Assert.True(aliases.TryGetProperty("r", out _), "Should have alias 'r'");
    Assert.True(aliases.TryGetProperty("f", out _), "Should have alias 'f'");
    Assert.True(aliases.TryGetProperty("s", out _), "Should have alias 's'");
  }

  [Fact]
  public void EmbeddedDotnetApiTemplateBooleanDefaultsDeserializeAsBooleans()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet-api"));
    var commands = document.RootElement.GetProperty("commands");

    var releasePushDefault = commands
      .GetProperty("release")
      .GetProperty("options")
      .GetProperty("push")
      .GetProperty("default");

    Assert.Equal(JsonValueKind.False, releasePushDefault.ValueKind);
  }

  [Fact]
  public void EmbeddedDotnetApiStageCommandRequiresStageArg()
  {
    using var document = JsonDocument.Parse(EmbeddedPolicyTemplates.ReadTemplate("dotnet-api"));
    var stageCommand = document.RootElement
      .GetProperty("commands")
      .GetProperty("stage");

    Assert.True(stageCommand.TryGetProperty("args", out var args), "stage command should declare args");
    Assert.True(args.TryGetProperty("stage", out var stageArg), "Should declare 'stage' arg");
    Assert.True(stageArg.GetProperty("required").GetBoolean(), "'stage' arg should be required");
  }
}

