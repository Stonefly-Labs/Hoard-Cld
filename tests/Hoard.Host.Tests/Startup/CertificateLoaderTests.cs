using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Certificate loading and refusal (FR-028, research R5, US2 sc.7).</summary>
public sealed class CertificateLoaderTests : IDisposable
{
    private const string WrongPassword = "Wr0ng-pa55word";

    private readonly TempDirectory work = new();
    private readonly TestCertificates certificates = new();

    public void Dispose()
    {
        certificates.Dispose();
        work.Dispose();
    }

    public static TheoryData<string> Loadable => ["combined", "separate key", "encrypted key", "pfx"];

    [Theory]
    [MemberData(nameof(Loadable))]
    public void Supported_formats_load_with_their_private_key(string shape)
    {
        var (args, password) = shape switch
        {
            "combined" => (Https(certificates.CombinedPem), null),
            "separate key" => (Https(certificates.CertificatePem, certificates.KeyPem), null),
            "encrypted key" => (Https(certificates.CertificatePem, certificates.EncryptedKeyPem), TestCertificates.Password),
            _ => (Https(certificates.Pfx), TestCertificates.Password),
        };

        var result = Options.Validate(work.Path, args, Password(password));

        Assert.Empty(result.Problems);
        var certificate = result.Options!.Tls.Certificate!;
        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(certificates.Thumbprint, certificate.Thumbprint);
    }

    [Fact]
    public void A_missing_file_fails()
    {
        AssertRefused(Https(work.Combine("missing.pem")), null, "does not exist");
    }

    [Fact]
    public void A_directory_fails()
    {
        AssertRefused(Https(work.Path), null, "not a regular file");
    }

    [Fact]
    public void A_fifo_fails_without_hanging()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "FIFOs are Unix only");
        var fifo = work.Combine("fifo.pem");
        using (var mkfifo = System.Diagnostics.Process.Start("mkfifo", [fifo]))
        {
            mkfifo.WaitForExit();
        }

        AssertRefused(Https(fifo), null, "not a regular file");
    }

    [Fact]
    public void A_file_larger_than_one_mebibyte_fails()
    {
        var big = work.Write("big.pem", "-----BEGIN CERTIFICATE-----\n" + new string('A', 1024 * 1024));

        AssertRefused(Https(big), null, "larger than 1 MiB");
    }

    [Fact]
    public void A_wrong_pfx_password_fails_without_echoing_it()
    {
        AssertRefused(Https(certificates.Pfx), WrongPassword, "PKCS#12");
    }

    [Fact]
    public void A_wrong_key_password_fails_without_echoing_it()
    {
        AssertRefused(Https(certificates.CertificatePem, certificates.EncryptedKeyPem), WrongPassword, "private key");
    }

    [Fact]
    public void An_encrypted_key_without_a_password_fails_asking_for_one()
    {
        AssertRefused(Https(certificates.CertificatePem, certificates.EncryptedKeyPem), null, "listen.tls.password");
    }

    [Fact]
    public void A_pem_certificate_without_a_key_fails()
    {
        AssertRefused(Https(certificates.CertificatePem), null, "private key");
    }

    [Fact]
    public void A_pfx_without_a_private_key_fails()
    {
        AssertRefused(Https(certificates.PfxWithoutKey), TestCertificates.Password, "no private key");
    }

    [Fact]
    public void A_key_that_does_not_match_the_certificate_fails()
    {
        AssertRefused(Https(certificates.CertificatePem, certificates.MismatchedKeyPem), null, "does not match");
    }

    [Fact]
    public void An_expired_certificate_fails()
    {
        AssertRefused(Https(certificates.ExpiredPem), null, "expired on");
    }

    [Fact]
    public void A_certificate_not_yet_valid_fails()
    {
        AssertRefused(Https(certificates.NotYetValidPem), null, "not valid until");
    }

    [Fact]
    public void A_missing_key_file_fails_naming_the_key_option()
    {
        var problem = Assert.Single(Options.Validate(work.Path, Https(certificates.CertificatePem, work.Combine("missing.pem"))).Problems);

        Assert.Equal("command line --listen:tls:keyFile", problem.Location);
        Assert.Contains("does not exist", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_certificate_creates_no_data_directory()
    {
        Assert.NotEmpty(Options.Validate(work.Path, Https(certificates.ExpiredPem)).Problems);

        Assert.False(Directory.Exists(work.Combine("data")));
    }

    private static string[] Https(string certificate, string? key = null) =>
        ["--listen:tls:mode", "https", "--listen:tls:certificateFile", certificate, .. key is null ? Array.Empty<string>() : ["--listen:tls:keyFile", key]];

    private static Dictionary<string, string>? Password(string? password) =>
        password is null ? null : new() { ["HOARD_LISTEN__TLS__PASSWORD"] = password };

    private void AssertRefused(string[] args, string? password, string reason)
    {
        var result = Options.Validate(work.Path, args, Password(password));

        Assert.Null(result.Options);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("command line --listen:tls:certificateFile", problem.Location);
        Assert.StartsWith("certificate could not be opened: ", problem.Message, StringComparison.Ordinal);
        Assert.Contains(reason, problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TestCertificates.Password, problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", problem.Message, StringComparison.Ordinal);
    }
}
