using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Cross-option TLS rules (FR-027–FR-029, ADR-0002, data-model rule 3).</summary>
public sealed class TlsRulesTests : IDisposable
{
    private readonly TempDirectory work = new();
    private readonly TestCertificates certificates = new();

    public void Dispose()
    {
        certificates.Dispose();
        work.Dispose();
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("10.1.2.3")]
    public void A_non_loopback_address_without_a_mode_fails_naming_both_modes(string address)
    {
        var problem = Assert.Single(Options.Validate(work.Path, ["--listen:address", address]).Problems);

        Assert.Equal("command line --listen:address", problem.Location);
        Assert.Contains("'https'", problem.Message, StringComparison.Ordinal);
        Assert.Contains("'proxy'", problem.Message, StringComparison.Ordinal);
        Assert.Contains("HOARD_LISTEN__TLS__MODE", problem.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(work.Combine("data")));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Loopback_needs_no_mode_and_accepts_either(string address)
    {
        Assert.Equal(TlsMode.None, Options.Valid(work.Path, "--listen:address", address).Tls.Mode);
        Assert.Equal(TlsMode.Proxy, Options.Valid(work.Path, "--listen:address", address, "--listen:tls:mode", "proxy").Tls.Mode);
        Assert.Equal(TlsMode.Https, Options.Valid(work.Path, "--listen:address", address, "--listen:tls:mode", "https",
            "--listen:tls:certificateFile", certificates.CombinedPem).Tls.Mode);
    }

    [Theory]
    [InlineData("proxy", "Proxy")]
    [InlineData("PROXY", "Proxy")]
    [InlineData("Https", "Https")]
    public void Modes_match_ignoring_case(string mode, string expectedMode)
    {
        var expected = Enum.Parse<TlsMode>(expectedMode);
        string[] certificate = expected == TlsMode.Https ? ["--listen:tls:certificateFile", certificates.CombinedPem] : [];

        Assert.Equal(expected, Options.Valid(work.Path, ["--listen:address", "0.0.0.0", "--listen:tls:mode", mode, .. certificate]).Tls.Mode);
    }

    [Theory]
    [InlineData("tls")]
    [InlineData("http")]
    [InlineData("none")]
    [InlineData("")]
    public void Other_modes_are_malformed(string mode)
    {
        var problems = Options.Validate(work.Path, [$"--listen:tls:mode={mode}"]).Problems;

        var problem = Assert.Single(problems);
        Assert.Equal("command line --listen:tls:mode", problem.Location);
        Assert.Contains("'https' or 'proxy'", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--listen:tls:certificateFile")]
    [InlineData("--listen:tls:keyFile")]
    public void Certificate_options_without_https_are_contradictory(string option)
    {
        foreach (string[] mode in new[] { Array.Empty<string>(), ["--listen:tls:mode", "proxy"] })
        {
            var problem = Assert.Single(Options.Validate(work.Path, [option, certificates.CombinedPem, .. mode]).Problems);

            Assert.Equal($"command line {option}", problem.Location);
            Assert.Contains("is not 'https'", problem.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_password_without_https_is_contradictory()
    {
        var problem = Assert.Single(Options.Validate(work.Path, ["--listen:tls:mode", "proxy"],
            new() { ["HOARD_LISTEN__TLS__PASSWORD"] = "s3cret" }).Problems);

        Assert.Equal("environment HOARD_LISTEN__TLS__PASSWORD", problem.Location);
        Assert.DoesNotContain("s3cret", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Https_without_a_certificate_fails()
    {
        var problem = Assert.Single(Options.Validate(work.Path, ["--listen:tls:mode", "https"]).Problems);

        Assert.Equal("command line --listen:tls:mode", problem.Location);
        Assert.Contains("no certificate", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_file_with_a_pkcs12_certificate_is_contradictory()
    {
        var problem = Assert.Single(Options.Validate(work.Path,
            ["--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.Pfx, "--listen:tls:keyFile", certificates.KeyPem],
            new() { ["HOARD_LISTEN__TLS__PASSWORD"] = TestCertificates.Password }).Problems);

        Assert.Equal("command line --listen:tls:keyFile", problem.Location);
        Assert.Contains("PKCS#12", problem.Message, StringComparison.Ordinal);
    }
}
