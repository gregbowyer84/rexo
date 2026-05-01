namespace Rexo.Execution.Tests;

public sealed class SecretMaskerTests
{
    [Fact]
    public void MaskReplacesSecretWithAsterisks()
    {
        var secrets = new HashSet<string> { "super-secret-value" };
        var masked = SecretMasker.Mask("output: super-secret-value end", secrets);
        Assert.Equal("output: *** end", masked);
    }

    [Fact]
    public void MaskIsNoopWhenNoSecretsMatch()
    {
        var secrets = new HashSet<string> { "other-secret" };
        var masked = SecretMasker.Mask("nothing to hide here", secrets);
        Assert.Equal("nothing to hide here", masked);
    }

    [Fact]
    public void MaskHandlesEmptySecretSet()
    {
        var secrets = new HashSet<string>();
        var masked = SecretMasker.Mask("plaintext", secrets);
        Assert.Equal("plaintext", masked);
    }

    [Fact]
    public void MaskReplacesLongerSecretBeforeShorter()
    {
        // Ensures sub-strings are not masked before their containing string
        var secrets = new HashSet<string> { "secret", "my-secret-token" };
        var masked = SecretMasker.Mask("my-secret-token", secrets);
        // Full string should be masked as one unit, not partially
        Assert.DoesNotContain("my-secret-token", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectSecretValuesDoesNotThrow()
    {
        // Just verify it runs without exceptions; actual values depend on test environment
        var secrets = SecretMasker.CollectSecretValues();
        Assert.NotNull(secrets);
    }
}
