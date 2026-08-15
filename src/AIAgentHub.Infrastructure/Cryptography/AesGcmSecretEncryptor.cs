using System.Runtime.InteropServices;
using System.Security.Cryptography;

using AIAgentHub.Application.Security;

namespace AIAgentHub.Infrastructure.Cryptography;

public interface IMasterKeyProvider
{
    public byte[] GetMasterKey();
}

public sealed class MasterKeyProvider : IMasterKeyProvider
{
    private static readonly Lock Lock = new();
    private byte[]? _masterKey;

    public byte[] GetMasterKey()
    {
        if (_masterKey != null)
        {
            return _masterKey;
        }

        lock (Lock)
        {
            if (_masterKey != null)
            {
                return _masterKey;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var keyDir = Path.Combine(localAppData, "AIAgentHub", "Keys");
            if (!Directory.Exists(keyDir))
            {
                _ = Directory.CreateDirectory(keyDir);
            }

            var keyPath = Path.Combine(keyDir, "master.key");

            if (File.Exists(keyPath))
            {
                var encrypted = File.ReadAllBytes(keyPath);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        _masterKey = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                        return _masterKey;
                    }
                    catch
                    {
                        // fallback to raw if DPAPI failed
                        _masterKey = encrypted;
                        return _masterKey;
                    }
                }
                else
                {
                    _masterKey = encrypted;
                    return _masterKey;
                }
            }

            // Generate new 32-byte AES-256 Master Key
            var newKey = RandomNumberGenerator.GetBytes(32);
            var toSave = newKey;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    toSave = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
                }
                catch
                {
                    toSave = newKey;
                }
            }

            File.WriteAllBytes(keyPath, toSave);
            _masterKey = newKey;
            return _masterKey;
        }
    }
}

public sealed class AesGcmSecretEncryptor(IMasterKeyProvider masterKeyProvider) : ISecretEncryptor
{
    private readonly IMasterKeyProvider _masterKeyProvider = masterKeyProvider;

    public (string CiphertextBase64, string NonceBase64, string TagBase64) Encrypt(string plainSecret)
    {
        var key = _masterKeyProvider.GetMasterKey();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainSecret ?? "");
        var nonce = RandomNumberGenerator.GetBytes(12); // standard 12-byte GCM nonce
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[16]; // 16-byte authentication tag

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

        return (
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag)
        );
    }

    public string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64)
    {
        var key = _masterKeyProvider.GetMasterKey();
        var ciphertext = Convert.FromBase64String(ciphertextBase64);
        var nonce = Convert.FromBase64String(nonceBase64);
        var tag = Convert.FromBase64String(tagBase64);
        var plainBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
