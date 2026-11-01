using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Process;

public sealed class FirstRunTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task First_run_logs_the_summary_and_creates_the_data_directory()
    {
        var port = Port();
        await using var host = HostProcess.Start(work.Path, ["--listen:port", port]);

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);

        var output = host.Output;
        Assert.Matches(@"Hoard \S+ started\.", output);
        Assert.Contains($"Listening:      http://localhost:{port} (bound: 127.0.0.1:{port}", output, StringComparison.Ordinal);
        Assert.Contains($"Data directory: {work.Combine("data")} (created)", output, StringComparison.Ordinal);
        Assert.Contains("listen.address=localhost (default)", output, StringComparison.Ordinal);
        Assert.Contains($"listen.port={port} (command line --listen:port)", output, StringComparison.Ordinal);
        Assert.Contains("data.path=./data (default)", output, StringComparison.Ordinal);
        Assert.Contains("Config file:    none", output, StringComparison.Ordinal);
        Assert.DoesNotContain("reachable from the network", output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(work.Combine("data")));
    }

    [Fact]
    public async Task A_second_run_reports_the_existing_data_directory()
    {
        var port = Port();
        Directory.CreateDirectory(work.Combine("data"));
        await using var host = HostProcess.Start(work.Path, ["--listen:port", port]);

        Assert.True(await host.WaitForOutputAsync("(existing)"), host.Output);
    }

    [Fact]
    public async Task Framework_environment_and_settings_cannot_widen_the_listener()
    {
        var port = Port();
        var wide = FreePort.Get();
        work.Write("appsettings.json", $$"""{ "Kestrel": { "Endpoints": { "Wide": { "Url": "http://0.0.0.0:{{wide}}" } } } }""");
        await using var host = HostProcess.Start(work.Path, ["--listen:port", port], new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = $"http://0.0.0.0:{wide}",
            ["ASPNETCORE_PREFERHOSTINGURLS"] = "true",
            ["Kestrel__Endpoints__X__Url"] = $"http://0.0.0.0:{wide}",
        });

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);

        Assert.False(FreePort.IsListening(IPAddress.Loopback, wide));
        Assert.True(FreePort.IsListening(IPAddress.Loopback, int.Parse(port, CultureInfo.InvariantCulture)));
        Assert.DoesNotContain("reachable from the network", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_framework_urls_argument_is_refused()
    {
        var wide = FreePort.Get();
        await using var host = HostProcess.Start(work.Path, ["--urls", $"http://0.0.0.0:{wide}"]);

        Assert.Equal(2, await host.WaitForExitAsync());
        Assert.Contains("command line --urls: unknown option", host.StandardError, StringComparison.Ordinal);
        Assert.False(Directory.Exists(work.Combine("data")));
    }

    private static string Port() => FreePort.Get().ToString(CultureInfo.InvariantCulture);
}

/// <summary>The one process test that uses the real default port; it must not run alongside anything else on 8484.</summary>
[CollectionDefinition(nameof(DefaultPort), DisableParallelization = true)]
public sealed class DefaultPort;

[Collection(nameof(DefaultPort))]
public sealed class DefaultPortTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task With_no_configuration_Hoard_listens_on_loopback_8484()
    {
        Assert.SkipWhen(FreePort.IsListening(IPAddress.Loopback, 8484), "something else is listening on 8484");
        await using var host = HostProcess.Start(work.Path);

        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);

        Assert.Contains("listen.port=8484 (default)", host.Output, StringComparison.Ordinal);
        using var client = new HttpClient();
        Assert.Equal("Healthy", await client.GetStringAsync("http://127.0.0.1:8484/health/ready", TestContext.Current.CancellationToken));
        if (Socket.OSSupportsIPv6)
        {
            Assert.Contains("[::1]:8484", host.Output, StringComparison.Ordinal);
        }
    }
}
