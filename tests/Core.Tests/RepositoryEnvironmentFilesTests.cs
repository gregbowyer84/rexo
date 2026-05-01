namespace Rexo.Core.Tests;

using Rexo.Core.Environment;
using Rexo.Core.Models;

public sealed class RepositoryEnvironmentFilesTests
{
    [Fact]
    public void LoadMergesRootThenRexoEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rexo-envfiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ".rexo"));

        try
        {
            File.WriteAllText(Path.Combine(dir, ".env"), "A=from-root\nB=from-root\n");
            File.WriteAllText(Path.Combine(dir, ".rexo", ".env"), "B=from-rexo\nC=from-rexo\n");

            var values = RepositoryEnvironmentFiles.Load(dir);

            Assert.Equal("from-root", values["A"]);
            Assert.Equal("from-rexo", values["B"]);
            Assert.Equal("from-rexo", values["C"]);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void ExecutionContextEnvironmentResolvesProcessOverFile()
    {
        const string key = "REXO_TEST_ENV_PRECEDENCE";
        var original = System.Environment.GetEnvironmentVariable(key);

        var dir = Path.Combine(Path.GetTempPath(), $"rexo-envctx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ".rexo"));

        try
        {
            File.WriteAllText(Path.Combine(dir, ".env"), $"{key}=from-root\n");
            File.WriteAllText(Path.Combine(dir, ".rexo", ".env"), $"{key}=from-rexo\n");
            System.Environment.SetEnvironmentVariable(key, "from-process");

            var context = ExecutionContext.Empty(dir);
            var resolved = context.GetEnvironmentValue(key);

            Assert.Equal("from-process", resolved);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, original);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
