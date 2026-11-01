using System.Globalization;
using System.Net;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Process;

/// <summary>Explicit exposure with a declared TLS posture and a warning (US3 sc.1–4, FR-010, FR-027).</summary>
public sealed class ExposureTests : IDisposable
{
    private const string Warning = "Hoard is listening on 0.0.0.0, which is reachable from the network. Package reads are anonymous";
    private const string ProxySentence = "relies on a TLS-terminating reverse proxy in front of it.";

    private readonly TempDirectory work = new();
    private readonly int port = FreePort.Get();

    private string Port => port.ToString(CultureInfo.InvariantCulture);

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task All_interfaces_without_a_mode_is_refused_naming_both_modes()
    {
        await using var host = HostProcess.Start(work.Path, ["--listen:address", "0.0.0.0", "--listen:port", Port]);

        Assert.Equal(2, await host.WaitForExitAsync());
        Assert.Contains("'https'", host.StandardError, StringComparison.Ordinal);
        Assert.Contains("'proxy'", host.StandardError, StringComparison.Ordinal);
        Assert.False(FreePort.IsListening(IPAddress.Loopback, port));
    }

    public static TheoryData<string> Sources => ["command line", "environment", "file"];

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task Proxy_mode_serves_on_all_interfaces_and_warns_once(string source)
    {
        string[] args = ["--listen:port", Port];
        Dictionary<string, string>? environment = null;
        switch (source)
        {
            case "command line":
                args = [.. args, "--listen:address", "0.0.0.0", "--listen:tls:mode", "proxy"];
                break;
            case "environment":
                environment = new() { ["HOARD_LISTEN__ADDRESS"] = "0.0.0.0", ["HOARD_LISTEN__TLS__MODE"] = "proxy" };
                break;
            default:
                work.Write("hoard.json", """{ "listen": { "address": "0.0.0.0", "tls": { "mode": "proxy" } } }""");
                break;
        }

        await using var host = HostProcess.Start(work.Path, args, environment);

        Assert.True(await host.WaitForOutputAsync(ProxySentence), host.Output);
        Assert.Contains($"Listening:      http://0.0.0.0:{port} (TLS mode proxy)", host.Output, StringComparison.Ordinal);
        Assert.Equal(2, host.Output.Split(Warning).Length);
        Assert.True(FreePort.IsListening(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task Loopback_logs_no_warning()
    {
        await using var host = HostProcess.Start(work.Path, ["--listen:port", Port, "--listen:tls:mode", "proxy"]);

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("reachable from the network", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Certificate_options_in_proxy_mode_are_refused()
    {
        work.Write("cert.pem", "x");
        await using var host = HostProcess.Start(work.Path, ["--listen:port", Port, "--listen:tls:mode", "proxy", "--listen:tls:certificateFile", "cert.pem"]);

        Assert.Equal(2, await host.WaitForExitAsync());
        Assert.Contains("command line --listen:tls:certificateFile: is set, but listen.tls.mode is not 'https'", host.StandardError, StringComparison.Ordinal);
    }
}
