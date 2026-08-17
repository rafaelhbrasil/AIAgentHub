using AIAgentHub.Infrastructure.Cryptography;

namespace AgentHub.UnitTests.Infrastructure.Cryptography;

public sealed class AesGcmSecretEncryptorTests
{
    [Fact]
    public void AesGcm_EncryptAndDecrypt_ShouldRestoreOriginalSecret()
    {
        var keyProvider = new MasterKeyProvider();
        var encryptor = new AesGcmSecretEncryptor(keyProvider);

        var originalSecret = "sk-ant-api03-abcdef123456789";
        var (ciphertext, nonce, tag) = encryptor.Encrypt(originalSecret);

        Assert.NotEmpty(ciphertext);
        Assert.NotEmpty(nonce);
        Assert.NotEmpty(tag);

        var decrypted = encryptor.Decrypt(ciphertext, nonce, tag);
        Assert.Equal(originalSecret, decrypted);
    }
}
