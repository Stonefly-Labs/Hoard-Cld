using System.Net;
using System.Security.Cryptography.X509Certificates;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Features.Health;

/// <summary>HTTPS only, with the configured certificate (FR-028, SC-008, research R5).</summary>
public sealed class HttpsEndpointTests : IDisposable
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
    public async Task Https_serves_the_configured_certificate(string address)
    {
        var options = Options.Loopback(work.Path, out var port, "--listen:address", address, "--listen:tls:mode", "https",
            "--listen:tls:certificateFile", certificates.CertificatePem, "--listen:tls:keyFile", certificates.KeyPem);
        await using var app = HoardApp.Build(options);
        await app.StartAsync(TestContext.Current.CancellationToken);

        string? served = null;
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                served = certificate?.Thumbprint;
                return true;
            },
        };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync($"https://127.0.0.1:{port}/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(certificates.Thumbprint, served);
        Assert.Equal([$"https://{address}:{port}"], app.Urls);
    }

    [Fact]
    public async Task Plain_http_to_the_https_port_gets_no_normal_response()
    {
        var options = Options.Loopback(work.Path, out var port, "--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.CombinedPem);
        await using var app = HoardApp.Build(options);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        HttpResponseMessage? response = null;
        try
        {
            response = await client.GetAsync($"http://127.0.0.1:{port}/health/ready", TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException)
        {
        }

        Assert.True(response is null || response.StatusCode != HttpStatusCode.OK, $"plain HTTP got {response?.StatusCode}");
    }

    [Fact]
    public void The_loaded_certificate_is_the_configured_one()
    {
        var options = Options.Valid(work.Path, "--listen:tls:mode", "https", "--listen:tls:certificateFile", certificates.CombinedPem);

        Assert.Equal(certificates.Thumbprint, options.Tls.Certificate!.Thumbprint);
        Assert.IsType<X509Certificate2>(options.Tls.Certificate);
    }
}
