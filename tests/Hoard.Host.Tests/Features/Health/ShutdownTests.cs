using System.Net;
using Hoard.Host.Features.Health;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Options = Hoard.Host.Tests.Support.Options;

namespace Hoard.Host.Tests.Features.Health;

/// <summary>Not ready once stopping; in-flight requests drain (US5 sc.3, FR-022, research R8).</summary>
public sealed class ShutdownTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task Readiness_turns_unhealthy_once_stopping_begins()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out _));
        await app.StartAsync(TestContext.Current.CancellationToken);
        var health = app.Services.GetRequiredService<HealthCheckService>();

        Assert.Equal(HealthStatus.Healthy, (await Ready(health)).Status);

        app.Lifetime.StopApplication();

        Assert.Equal(HealthStatus.Unhealthy, (await Ready(health)).Status);
    }

    [Fact]
    public async Task Readiness_is_unhealthy_before_start()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out _));

        Assert.Equal(HealthStatus.Unhealthy, (await Ready(app.Services.GetRequiredService<HealthCheckService>())).Status);
    }

    [Fact]
    public async Task An_in_flight_request_completes_and_new_connections_are_refused()
    {
        var token = TestContext.Current.CancellationToken;
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out var port, "--listen:address", "127.0.0.1"));
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        app.MapGet("/slow", async () =>
        {
            entered.SetResult();
            await release.Task;
            return Results.Text("done");
        });
        await app.StartAsync(token);

        using var client = new HttpClient();
        var inFlight = client.GetAsync($"http://127.0.0.1:{port}/slow", token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);

        var stopping = app.StopAsync(CancellationToken.None);
        await Task.Delay(300, token);

        Assert.False(FreePort.IsListening(IPAddress.Loopback, port));
        Assert.False(stopping.IsCompleted);

        release.SetResult();
        var response = await inFlight;
        await stopping.WaitAsync(TimeSpan.FromSeconds(10), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("done", await response.Content.ReadAsStringAsync(token));
    }

    [Fact]
    public async Task The_shutdown_timeout_is_five_seconds()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out _));

        Assert.Equal(TimeSpan.FromSeconds(5), app.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Fact]
    public async Task A_stop_requested_before_start_exits_cleanly_without_serving()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        using var error = new StringWriter();

        var code = await HoardStartup.RunAsync(["--listen:port", FreePort.Get().ToString(System.Globalization.CultureInfo.InvariantCulture)],
            new Dictionary<string, string>(), work.Path, error, stop.Token);

        Assert.Equal(0, code);
        Assert.Empty(error.ToString());
    }

    private static Task<HealthReport> Ready(HealthCheckService health) =>
        health.CheckHealthAsync(r => r.Tags.Contains(HealthEndpoints.ReadyTag), TestContext.Current.CancellationToken);
}
