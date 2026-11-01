using System.Net;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;
using Microsoft.AspNetCore.Builder;

namespace Hoard.Host.Tests.Features.Health;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly TempDirectory work = new();
    private readonly HttpClient client = new();
    private WebApplication app = null!;

    public async ValueTask InitializeAsync()
    {
        var options = Options.Loopback(work.Path, out var port);
        app = HoardApp.Build(options);
        await app.StartAsync(TestContext.Current.CancellationToken);
        client.BaseAddress = new Uri($"http://127.0.0.1:{port}");
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
        work.Dispose();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_returns_healthy_as_plain_text(string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Head_returns_ok(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, path);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/anything")]
    [InlineData("/health/other")]
    public async Task Other_paths_return_not_found(string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Other_methods_return_method_not_allowed(string path)
    {
        var response = await client.PostAsync(path, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
