using System.Security.Cryptography;
using System.Text;

using AIAgentHub.Application.Security;

using Konscious.Security.Cryptography;

namespace AIAgentHub.Infrastructure.Cryptography;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 4;
    private const int MemorySizeKb = 65536; // 64 MB
    private const int DegreeOfParallelism = 2;

    public (string HashBase64, string SaltBase64) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = GenerateHash(password, salt);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string password, string hashBase64, string saltBase64)
    {
        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(hashBase64);
            var actualHash = GenerateHash(password, salt);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }

    public (string HashBase64, string PlainCode) GenerateRecoveryCode()
    {
        // 16-character alphanumeric recovery code formatted as XXXX-XXXX-XXXX-XXXX
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var base32Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new StringBuilder();

        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                _ = sb.Append('-');
            }

            _ = sb.Append(base32Chars[randomBytes[i] % base32Chars.Length]);
        }

        var plainCode = sb.ToString();
        var normalized = plainCode.Replace("-", "").Trim().ToUpperInvariant();
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));

        return (Convert.ToBase64String(hash), plainCode);
    }

    public bool VerifyRecoveryCode(string plainCode, string hashBase64)
    {
        if (string.IsNullOrWhiteSpace(plainCode) || string.IsNullOrWhiteSpace(hashBase64))
        {
            return false;
        }

        try
        {
            var normalized = plainCode.Replace("-", "").Trim().ToUpperInvariant();
            using var sha = SHA256.Create();
            var actualHash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var expectedHash = Convert.FromBase64String(hashBase64);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GenerateHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb
        };

        return argon2.GetBytes(HashSizeBytes);
    }
}
