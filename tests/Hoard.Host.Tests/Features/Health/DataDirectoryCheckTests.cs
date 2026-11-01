using System.Net;
using Hoard.Host.Features.Health;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hoard.Host.Tests.Features.Health;

/// <summary>Readiness follows the data directory, with no latch and no caching (US5 sc.2, FR-018, research R6).</summary>
public sealed class DataDirectoryCheckTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task Readiness_fails_while_the_directory_is_gone_and_recovers_after()
    {
        var options = Options.Loopback(work.Path, out var port);
        await using var app = HoardApp.Build(options);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var token = TestContext.Current.CancellationToken;

        Directory.Delete(options.DataDirectory.Path);

        var ready = await client.GetAsync("/health/ready", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(token));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", token)).StatusCode);

        Directory.CreateDirectory(options.DataDirectory.Path);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", token)).StatusCode);
    }

    [Fact]
    public async Task Readiness_fails_while_the_directory_is_unwritable()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess, "needs Unix file modes as a non-root user");
        var options = Options.Loopback(work.Path, out var port);
        await using var app = HoardApp.Build(options);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

#pragma warning disable CA1416 // guarded by the skip above
        File.SetUnixFileMode(options.DataDirectory.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_probes_share_one_check()
    {
        var runs = 0;
        using var release = new ManualResetEventSlim();
        var check = new DataDirectoryCheck(() =>
        {
            Interlocked.Increment(ref runs);
            release.Wait(TimeSpan.FromSeconds(5));
        });

        var probes = Enumerable.Range(0, 8).Select(_ => check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)).ToList();
        await Task.Delay(200, TestContext.Current.CancellationToken);
        release.Set();
        var results = await Task.WhenAll(probes);

        Assert.Equal(1, runs);
        Assert.All(results, r => Assert.Equal(HealthStatus.Healthy, r.Status));
    }

    [Fact]
    public async Task Results_are_not_cached_between_probes()
    {
        var runs = 0;
        var check = new DataDirectoryCheck(() => Interlocked.Increment(ref runs));

        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task A_slow_check_reports_unhealthy_after_the_timeout()
    {
        using var release = new ManualResetEventSlim();
        var check = new DataDirectoryCheck(() => release.Wait(TimeSpan.FromSeconds(10)), TimeSpan.FromMilliseconds(200));

        var started = DateTime.UtcNow;
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        release.Set();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void The_timeout_is_two_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), DataDirectoryCheck.Timeout);
    }

    [Fact]
    public async Task A_failing_write_check_reports_unhealthy()
    {
        var directory = DataDirectory.Create(work.Combine("data"));
        Directory.Delete(directory.Path);

        var result = await new DataDirectoryCheck(directory.WriteCheck).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
