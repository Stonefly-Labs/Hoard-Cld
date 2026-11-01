using System.Net;
using System.Net.Sockets;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Hoard.Host.Tests.Startup;

public sealed class HoardAppTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public async Task The_app_reads_no_framework_configuration_source()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out _));

        var providers = Flatten(((IConfigurationRoot)app.Configuration).Providers).ToList();

        Assert.DoesNotContain(providers, p => p is EnvironmentVariablesConfigurationProvider
            or CommandLineConfigurationProvider
            or FileConfigurationProvider);
    }

    [Fact]
    public async Task Localhost_is_one_endpoint_on_both_loopback_addresses()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out var port));
        await app.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"http://localhost:{port}"], app.Urls);
        Assert.True(FreePort.IsListening(IPAddress.Loopback, port));
        if (Socket.OSSupportsIPv6)
        {
            Assert.True(FreePort.IsListening(IPAddress.IPv6Loopback, port));
        }
    }

    [Fact]
    public async Task An_address_is_one_endpoint_on_that_address_only()
    {
        await using var app = HoardApp.Build(Options.Loopback(work.Path, out var port, "--listen:address", "127.0.0.1"));
        await app.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"http://127.0.0.1:{port}"], app.Urls);
        if (Socket.OSSupportsIPv6)
        {
            Assert.False(FreePort.IsListening(IPAddress.IPv6Loopback, port));
        }
    }

    private static IEnumerable<IConfigurationProvider> Flatten(IEnumerable<IConfigurationProvider> providers) =>
        providers.SelectMany(p => p is ChainedConfigurationProvider chained
            ? Flatten(((IConfigurationRoot)chained.Configuration).Providers).Prepend(p)
            : [p]);
}
