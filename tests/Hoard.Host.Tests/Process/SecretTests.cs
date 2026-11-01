using System.Globalization;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Process;

/// <summary>A planted secret never appears in any output (FR-008, FR-024, SC-007).</summary>
public sealed class SecretTests : IDisposable
{
    private const string Secret = "Pl4nted-S3cret-7f3a";

    private readonly TempDirectory work = new();
    private readonly TestCertificates certificates = new();
    private readonly string port = FreePort.Get().ToString(CultureInfo.InvariantCulture);

    public void Dispose()
    {
        certificates.Dispose();
        work.Dispose();
    }

    [Theory]
    [InlineData(Secret)]
    [InlineData("--" + Secret)]
    [InlineData("-" + Secret)]
    [InlineData("/" + Secret)]
    public async Task The_password_on_the_command_line_is_refused_and_never_echoed(string password)
    {
        var host = await Run(["--listen:tls:password", password]);

        Assert.Contains("HOARD_LISTEN__TLS__PASSWORD", host.StandardError, StringComparison.Ordinal);
        Assert.Contains("config file", host.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_inline_and_repeated_password_forms_are_never_echoed()
    {
        await Run([$"--listen:tls:password={Secret}"]);
        await Run(["--listen:tls:password", Secret, "--listen:tls:password", Secret]);
    }

    [Fact]
    public async Task A_password_of_the_wrong_type_in_the_file_is_never_echoed()
    {
        work.Write("hoard.json", $$"""{ "listen": { "tls": { "password": ["{{Secret}}"] } } }""");

        await Run([]);
    }

    [Fact]
    public async Task A_password_with_proxy_mode_is_never_echoed()
    {
        await Run(["--listen:tls:mode", "proxy"], new() { ["HOARD_LISTEN__TLS__PASSWORD"] = Secret });
    }

    [Fact]
    public async Task A_wrong_pfx_password_is_never_echoed()
    {
        await Run(["--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.Pfx], new() { ["HOARD_LISTEN__TLS__PASSWORD"] = Secret });
    }

    [Fact]
    public async Task A_secret_as_a_positional_argument_or_an_unknown_value_is_never_echoed()
    {
        await Run([Secret]);
        await Run(["--listen:pasword", Secret]);
        await Run([$"--nope={Secret}"]);
        await Run([], new() { ["HOARD_LISTEN__PASWORD"] = Secret });
    }

    [Fact]
    public async Task A_secret_in_another_variable_never_appears_in_a_normal_run()
    {
        await using var host = HostProcess.Start(work.Path, ["--listen:port", port], new Dictionary<string, string> { ["MY_TOKEN"] = Secret });

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);
        Assert.DoesNotContain(Secret, host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("MY_TOKEN", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_correct_password_is_shown_only_as_set()
    {
        await using var host = HostProcess.Start(work.Path,
            ["--listen:port", port, "--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.Pfx],
            new Dictionary<string, string> { ["HOARD_LISTEN__TLS__PASSWORD"] = TestCertificates.Password });

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);
        Assert.Contains("listen.tls.password=(set) (environment HOARD_LISTEN__TLS__PASSWORD)", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestCertificates.Password, host.Output, StringComparison.Ordinal);
    }

    private async Task<HostProcess> Run(string[] args, Dictionary<string, string>? environment = null)
    {
        var host = HostProcess.Start(work.Path, ["--listen:port", port, .. args], environment);
        await using (host)
        {
            Assert.Equal(2, await host.WaitForExitAsync());
            Assert.DoesNotContain(Secret, host.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(port + " " + string.Join(' ', args), host.Output, StringComparison.Ordinal);
        }

        return host;
    }
}
