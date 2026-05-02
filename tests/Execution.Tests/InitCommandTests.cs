namespace Rexo.Execution.Tests;

using Rexo.Core.Models;

public sealed class InitCommandTests
{
    [Fact]
    public async Task InitCreatesConfigInDotRexoByDefaultWhenNonInteractive()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?> { ["yes"] = "true" },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.True(result.Success);
            var configPath = Path.Combine(dir, ".rexo", "rexo.json");
            Assert.True(File.Exists(configPath));
            // Default schema source is remote — no local schema file written.
            var schemaPath = Path.Combine(dir, ".rexo", "rexo.schema.json");
            Assert.False(File.Exists(schemaPath));
            var content = await File.ReadAllTextAsync(configPath);
            Assert.Contains("\"$schema\": \"https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json\"", content, StringComparison.Ordinal);
            Assert.Contains("\"schemaVersion\": \"1.0\"", content, StringComparison.Ordinal);
            Assert.Contains("\"commands\"", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitReturnsFailureWhenConfigExistsAndNoForce()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-exists-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var existingPath = Path.Combine(dir, "rexo.json");
            await File.WriteAllTextAsync(existingPath, "{}");

            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?> { ["yes"] = "true" },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitCreatesPolicyWhenRequested()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["with-policy"] = "true",
                    ["policy-template"] = "dotnet",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.True(result.Success);
            var policyPath = Path.Combine(dir, ".rexo", "policy.json");
            Assert.True(File.Exists(policyPath));
            var content = await File.ReadAllTextAsync(policyPath);
            Assert.Contains("dotnet-policy", content, StringComparison.Ordinal);
            Assert.Contains("\"ci\":", content, StringComparison.Ordinal);
            Assert.Contains("\"release\":", content, StringComparison.Ordinal);
            Assert.Contains("\"build\": \"ci\"", content, StringComparison.Ordinal);
            Assert.Contains("\"publish\": \"release\"", content, StringComparison.Ordinal);

            var configPath = Path.Combine(dir, ".rexo", "rexo.json");
            var configContent = await File.ReadAllTextAsync(configPath);
            Assert.DoesNotContain("\"build\":", configContent, StringComparison.Ordinal);
            Assert.Contains("\"compile\":", configContent, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitWithStandardPolicyRenamesCollidingStarterBuildCommand()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-standard-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["with-policy"] = "true",
                    ["policy-template"] = "standard",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.True(result.Success);
            var configPath = Path.Combine(dir, ".rexo", "rexo.json");
            var content = await File.ReadAllTextAsync(configPath);
            Assert.DoesNotContain("\"build\":", content, StringComparison.Ordinal);
            Assert.Contains("\"compile\":", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitFailsWhenPolicyTemplateIsInvalid()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-policy-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["with-policy"] = "true",
                    ["policy-template"] = "does-not-exist",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Invalid --policy-template", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitFailsWhenInstructionsPathEscapesRepository()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-instructions-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["with-instructions"] = "true",
                    ["instructions-path"] = "..\\outside.instructions.md",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Path must remain within the repository", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitFailsWhenInstructionsFileExistsAndNoForce()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-instructions-exists-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var instructionsPath = Path.Combine(dir, ".github", "instructions", "rexo.instructions.md");
            Directory.CreateDirectory(Path.GetDirectoryName(instructionsPath)!);
            await File.WriteAllTextAsync(instructionsPath, "existing");

            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["with-instructions"] = "true",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Instructions file already exists", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitFailsWhenLocationIsRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-root-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["location"] = "root",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("always creates .rexo/rexo.json", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task InitWithRemoteSchemaDoesNotCreateLocalSchemaFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-init-remote-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var registry = BuiltinCommandRegistration.CreateDefault();
            var executor = new DefaultCommandExecutor(registry);

            var invocation = new CommandInvocation(
                new Dictionary<string, string>(),
                new Dictionary<string, string?>
                {
                    ["yes"] = "true",
                    ["schema-source"] = "remote",
                },
                Json: false,
                JsonFile: null,
                WorkingDirectory: dir);

            var result = await executor.ExecuteAsync("init", invocation, CancellationToken.None);

            Assert.True(result.Success);
            var configPath = Path.Combine(dir, ".rexo", "rexo.json");
            var schemaPath = Path.Combine(dir, ".rexo", "rexo.schema.json");
            Assert.True(File.Exists(configPath));
            Assert.False(File.Exists(schemaPath));
            var content = await File.ReadAllTextAsync(configPath);
            Assert.Contains("\"$schema\": \"https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json\"", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
