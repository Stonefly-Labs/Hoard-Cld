using Hoard.Host.Features.Health;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Hoard.Host.Startup;

/// <summary>
/// Composes the host from validated options only (research R1). The empty builder reads no
/// framework configuration, so no environment variable, argument or settings file can add or widen
/// a listener; Kestrel gets exactly one endpoint.
/// </summary>
internal static class HoardApp
{
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Builds the app unstarted, so tests can map extra endpoints before starting it.</summary>
    public static WebApplication Build(HoardOptions options)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = [] });

        builder.WebHost.UseKestrelCore();
        builder.WebHost.PreferHostingUrls(false);
        builder.WebHost.ConfigureKestrel(kestrel => Listen(kestrel, options));

        builder.Logging.AddSimpleConsole(console => console.SingleLine = false);
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

        // It logs a failed start with the exception and stack trace; HoardStartup reports every
        // failure in its own words, since exception text can carry values (research R9).
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = ShutdownTimeout);
        builder.Services.AddRouting();
        builder.Services.AddHealth(options.DataDirectory);

        var app = builder.Build();
        app.MapHealth();
        return app;
    }

    private static void Listen(KestrelServerOptions kestrel, HoardOptions options)
    {
        var certificate = options.Tls.Mode == TlsMode.Https ? options.Tls.Certificate : null;
        Action<ListenOptions> configure = certificate is null ? _ => { } : endpoint => endpoint.UseHttps(certificate);
        if (options.Listen.Address.Ip is { } ip)
        {
            kestrel.Listen(ip, options.Listen.Port, configure);
        }
        else
        {
            kestrel.ListenLocalhost(options.Listen.Port, configure);
        }
    }
}
