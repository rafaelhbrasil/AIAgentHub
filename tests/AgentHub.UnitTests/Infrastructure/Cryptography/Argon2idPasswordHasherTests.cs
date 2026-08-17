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
}
