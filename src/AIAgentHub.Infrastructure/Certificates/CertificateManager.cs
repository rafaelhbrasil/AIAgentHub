using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AIAgentHub.Infrastructure.Certificates;

public interface ICertificateManager
{
    public X509Certificate2 GetOrCreateSelfSignedCertificate();
    public string GetCertificatePath();
}

public sealed class CertificateManager : ICertificateManager
{
    private const string CertPassword = "AIAgentHubLocalTlsCertPassword2026!";

    public string GetCertificatePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var certDir = Path.Combine(localAppData, "AIAgentHub", "Certs");
        if (!Directory.Exists(certDir))
        {
            _ = Directory.CreateDirectory(certDir);
        }

        return Path.Combine(certDir, "server.pfx");
    }

    public X509Certificate2 GetOrCreateSelfSignedCertificate()
    {
        var certPath = GetCertificatePath();
        if (File.Exists(certPath))
        {
            try
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(certPath, CertPassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                if (existing.NotAfter > DateTime.UtcNow.AddDays(7))
                {
                    return existing;
                }
            }
            catch
            {
                // Re-create if corrupted
            }
        }

        var cert = GenerateCertificate();
        var pfxBytes = cert.Export(X509ContentType.Pfx, CertPassword);
        File.WriteAllBytes(certPath, pfxBytes);

        return cert;
    }

    private static X509Certificate2 GenerateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName("CN=AIAgentHub Local Server, O=AI Agent Hub, OU=Development");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], // Server Authentication
                false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName.ToLowerInvariant());
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

        try
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                {
                    sanBuilder.AddIpAddress(ip);
                }
            }
        }
        catch
        {
            // Ignore DNS lookup errors
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2));

        return cert;
    }
}
