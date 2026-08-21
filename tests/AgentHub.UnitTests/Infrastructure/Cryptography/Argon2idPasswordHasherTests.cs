using AIAgentHub.Infrastructure.Cryptography;

namespace AgentHub.UnitTests.Infrastructure.Cryptography;

public sealed class Argon2idPasswordHasherTests
{
    [Fact]
    public void Argon2id_HashAndVerifyPassword_ShouldSucceed()
    {
        var hasher = new Argon2idPasswordHasher();
        var (hash, salt) = hasher.HashPassword("SuperSecretPassword123!");

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);

        var isValid = hasher.VerifyPassword("SuperSecretPassword123!", hash, salt);
        Assert.True(isValid);

        var isInvalid = hasher.VerifyPassword("WrongPassword", hash, salt);
        Assert.False(isInvalid);
    }

    [Fact]
    public void Argon2id_GenerateRecoveryCode_ShouldFormatCorrectly()
    {
        var hasher = new Argon2idPasswordHasher();
        var (hash, plainCode) = hasher.GenerateRecoveryCode();

        Assert.NotEmpty(hash);
        Assert.NotEmpty(plainCode);
        Assert.Contains("-", plainCode);
    }

    [Fact]
    public void Argon2id_VerifyRecoveryCode_ShouldValidateCorrectCode_AndRejectTampered()
    {
        var hasher = new Argon2idPasswordHasher();
        var (hash, plainCode) = hasher.GenerateRecoveryCode();

        // Exact match
        Assert.True(hasher.VerifyRecoveryCode(plainCode, hash));

        // Case insensitivity
        Assert.True(hasher.VerifyRecoveryCode(plainCode.ToLowerInvariant(), hash));

        // Dash insensitivity
        Assert.True(hasher.VerifyRecoveryCode(plainCode.Replace("-", ""), hash));

        // Invalid code
        Assert.False(hasher.VerifyRecoveryCode("INVALID-CODE-1234-5678", hash));

        // Empty / whitespace
        Assert.False(hasher.VerifyRecoveryCode("", hash));
        Assert.False(hasher.VerifyRecoveryCode("   ", hash));
        Assert.False(hasher.VerifyRecoveryCode(plainCode, ""));
    }
}
