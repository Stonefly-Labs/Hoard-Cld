using System.Globalization;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Process;

/// <summary>
/// Stop signals, restart and crash recovery (US5 sc.3–5, SC-006, FR-023). SIGTERM is Unix only;
/// Ctrl+C on Windows is not automatically tested (docs/DEVELOPMENT.md).
/// </summary>
public sealed class LifecycleTests : IDisposable
{
    private readonly TempDirectory work = new();
    private readonly string port = FreePort.Get().ToString(CultureInfo.InvariantCulture);

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task Sigterm_stops_cleanly_with_exit_code_0_within_10_seconds()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SIGTERM is Unix only");
        await using var host = HostProcess.Start(work.Path, ["--listen:port", port]);
        Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);

        host.SendSigterm();

        Assert.Equal(0, await host.WaitForExitAsync(TimeSpan.FromSeconds(10)));
        Assert.Contains("Hoard is stopping.", host.Output, StringComparison.Ordinal);
        Assert.Contains("Hoard stopped.", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_clean_restart_reuses_the_address_and_data_directory()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "SIGTERM is Unix only");
        string first;
        await using (var host = HostProcess.Start(work.Path, ["--listen:port", port]))
        {
            Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);
            first = host.Output;
            host.SendSigterm();
            Assert.Equal(0, await host.WaitForExitAsync(TimeSpan.FromSeconds(10)));
        }

        await using var again = HostProcess.Start(work.Path, ["--listen:port", port]);

        Assert.True(await again.WaitForOutputAsync("Config file:"), again.Output);
        Assert.Contains("(created)", first, StringComparison.Ordinal);
        Assert.Contains($"Data directory: {work.Combine("data")} (existing)", again.Output, StringComparison.Ordinal);
        Assert.Contains($"http://localhost:{port}", again.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_restart_after_a_kill_starts_normally()
    {
        await using (var host = HostProcess.Start(work.Path, ["--listen:port", port]))
        {
            Assert.True(await host.WaitForOutputAsync("Config file:"), host.Output);
        }

        await using var again = HostProcess.Start(work.Path, ["--listen:port", port]);

        Assert.True(await again.WaitForOutputAsync("(existing)"), again.Output);
        using var client = new HttpClient();
        Assert.Equal("Healthy", await client.GetStringAsync($"http://127.0.0.1:{port}/health/ready", TestContext.Current.CancellationToken));
    }
}
