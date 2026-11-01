using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Summary, exposure warning and problem report text (FR-010, FR-021, SC-004, contracts).</summary>
public sealed class StartupReportTests : IDisposable
{
    private readonly TempDirectory work = new();
    private readonly TestCertificates certificates = new();

    public void Dispose()
    {
        certificates.Dispose();
        work.Dispose();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Loopback_gets_no_exposure_warning(string address)
    {
        Assert.Null(StartupReport.ExposureWarning(Options.Valid(work.Path, "--listen:address", address)));
        Assert.Null(StartupReport.ExposureWarning(Options.Valid(work.Path, "--listen:address", address, "--listen:tls:mode", "proxy")));
    }

    [Fact]
    public void Proxy_exposure_warns_and_mentions_the_proxy()
    {
        var warning = StartupReport.ExposureWarning(Options.Valid(work.Path, "--listen:address", "0.0.0.0", "--listen:tls:mode", "proxy"));

        Assert.Equal(
            "Hoard is listening on 0.0.0.0, which is reachable from the network. Package reads are anonymous: anyone who can reach this address can use the cache. "
            + "Hoard serves plain HTTP here and relies on a TLS-terminating reverse proxy in front of it.",
            warning);
    }

    [Fact]
    public void Https_exposure_warns_without_the_proxy_sentence()
    {
        var warning = StartupReport.ExposureWarning(Options.Valid(work.Path, "--listen:address", "10.1.2.3", "--listen:tls:mode", "https",
            "--listen:tls:certificateFile", certificates.CombinedPem));

        Assert.NotNull(warning);
        Assert.StartsWith("Hoard is listening on 10.1.2.3, which is reachable from the network.", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("proxy", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void The_https_summary_shows_subject_and_expiry_and_hides_the_password()
    {
        var options = OptionValidator.Validate(
            OptionReader.Read(
                ["--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.Pfx],
                new Dictionary<string, string> { ["HOARD_LISTEN__TLS__PASSWORD"] = TestCertificates.Password },
                work.Path),
            work.Path,
            DateTimeOffset.UtcNow).Options!;

        var summary = StartupReport.Summary(options, ["127.0.0.1:8484"], "1.2.3");

        Assert.Contains("Listening:      https://localhost:8484 (TLS mode https, certificate CN=localhost, expires ", summary, StringComparison.Ordinal);
        Assert.Contains($"expires {options.Tls.Certificate!.NotAfter.ToUniversalTime():yyyy-MM-dd}", summary, StringComparison.Ordinal);
        Assert.Contains("listen.tls.password=(set) (environment HOARD_LISTEN__TLS__PASSWORD)", summary, StringComparison.Ordinal);
        Assert.DoesNotContain(TestCertificates.Password, summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_follows_the_contract_shape()
    {
        var options = Options.Valid(work.Path, "--listen:address", "0.0.0.0", "--listen:tls:mode", "proxy");

        var summary = StartupReport.Summary(options, ["0.0.0.0:8484"], "0.0.2-alpha");

        Assert.Equal(
            $"""
            Hoard 0.0.2-alpha started.
              Listening:      http://0.0.0.0:8484 (TLS mode proxy)
              Data directory: {work.Combine("data")} (created)
              Options:        listen.address=0.0.0.0 (command line --listen:address), listen.port=8484 (default), listen.tls.mode=proxy (command line --listen:tls:mode), data.path=./data (default)
              Config file:    none
            """.ReplaceLineEndings("\n"),
            summary);
    }

    [Fact]
    public void Localhost_lists_the_bound_addresses_and_ipv6_addresses_are_bracketed()
    {
        var localhost = StartupReport.Summary(Options.Valid(work.Path), ["127.0.0.1:8484", "[::1]:8484"], "1");
        var any6 = StartupReport.Summary(Options.Valid(work.Path, "--listen:address", "::", "--listen:tls:mode", "proxy"), ["[::]:8484"], "1");

        Assert.Contains("Listening:      http://localhost:8484 (bound: 127.0.0.1:8484, [::1]:8484)", localhost, StringComparison.Ordinal);
        Assert.Contains("Listening:      http://[::]:8484 (TLS mode proxy)", any6, StringComparison.Ordinal);
    }

    [Fact]
    public void The_problem_report_has_a_counted_heading_and_one_line_each()
    {
        var report = StartupReport.Problems([new("command line --a", "bad; fix"), new("environment HOARD_B", "worse; fix")]);

        Assert.Equal(
            """
            Hoard could not start: 2 problems in its configuration.
              - command line --a: bad; fix
              - environment HOARD_B: worse; fix

            """.ReplaceLineEndings("\n"),
            report);
        Assert.StartsWith("Hoard could not start: 1 problem in its configuration.", StartupReport.Problems([new("x", "y")]), StringComparison.Ordinal);
    }
}
