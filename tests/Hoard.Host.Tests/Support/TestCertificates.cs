using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hoard.Host.Tests.Support;

/// <summary>
/// Self-signed certificates in every shape the loader must accept or refuse, written to a temp
/// directory (research R12).
/// </summary>
internal sealed class TestCertificates : IDisposable
{
    public const string Password = "Pfx-pa55word!";

    private readonly TempDirectory directory = new();

    public TestCertificates()
    {
        using var valid = Create(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), out var key);
        Thumbprint = valid.Thumbprint;
        var certificatePem = valid.ExportCertificatePem();
        var keyPem = key.ExportPkcs8PrivateKeyPem();

        CombinedPem = directory.Write("combined.pem", certificatePem + "\n" + keyPem);
        CertificatePem = directory.Write("cert.pem", certificatePem);
        KeyPem = directory.Write("key.pem", keyPem);
        EncryptedKeyPem = directory.Write("key-encrypted.pem", key.ExportEncryptedPkcs8PrivateKeyPem(
            Password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000)));
        Pfx = directory.Combine("cert.pfx");
        File.WriteAllBytes(Pfx, valid.Export(X509ContentType.Pkcs12, Password));

        using var publicOnly = X509CertificateLoader.LoadCertificate(valid.RawData);
        PfxWithoutKey = directory.Combine("public.pfx");
        File.WriteAllBytes(PfxWithoutKey, publicOnly.Export(X509ContentType.Pkcs12, Password));

        using var other = RSA.Create(2048);
        MismatchedKeyPem = directory.Write("other-key.pem", other.ExportPkcs8PrivateKeyPem());

        using var expired = Create(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1), out var expiredKey);
        ExpiredPem = directory.Write("expired.pem", expired.ExportCertificatePem() + "\n" + expiredKey.ExportPkcs8PrivateKeyPem());
        expiredKey.Dispose();

        using var future = Create(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(30), out var futureKey);
        NotYetValidPem = directory.Write("future.pem", future.ExportCertificatePem() + "\n" + futureKey.ExportPkcs8PrivateKeyPem());
        futureKey.Dispose();

        key.Dispose();
    }

    public string Directory => directory.Path;

    public string Thumbprint { get; }

    public string CombinedPem { get; }

    public string CertificatePem { get; }

    public string KeyPem { get; }

    public string EncryptedKeyPem { get; }

    public string MismatchedKeyPem { get; }

    public string Pfx { get; }

    public string PfxWithoutKey { get; }

    public string ExpiredPem { get; }

    public string NotYetValidPem { get; }

    public void Dispose() => directory.Dispose();

    private static X509Certificate2 Create(DateTimeOffset notBefore, DateTimeOffset notAfter, out RSA key)
    {
        key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
