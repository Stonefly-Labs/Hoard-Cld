using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.AspNetCore.Connections;

namespace Hoard.Host.Startup;

/// <summary>
/// Validate → build → start → summary → wait → stop, with exit codes 0 (clean stop), 1 (bind or
/// unexpected failure) and 2 (configuration or precondition problem; nothing opened) (research R8, R9).
/// </summary>
internal static class HoardStartup
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        TextWriter error,
        CancellationToken stop)
    {
        try
        {
            var read = OptionReader.Read(args, environment, workingDirectory);
            var result = OptionValidator.Validate(read, workingDirectory, DateTimeOffset.UtcNow);
            if (result.Options is not { } options)
            {
                await error.WriteAsync(StartupReport.Problems(result.Problems));
                return 2;
            }

            using var certificate = options.Tls.Certificate;
            await using var app = HoardApp.Build(options);
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hoard.Host");
            try
            {
                await app.StartAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                logger.LogInformation("Hoard stopped before it started serving.");
                return 0;
            }
            catch (IOException e)
            {
                await error.WriteLineAsync(BindFailure(options, e));
                return 1;
            }

            using var stopping = app.Lifetime.ApplicationStopping.Register(() => logger.LogInformation("Hoard is stopping."));
            using var signal = stop.Register(app.Lifetime.StopApplication);

            logger.LogInformation("{Summary}", StartupReport.Summary(options, await BoundAddressesAsync(options.Listen), Version));
            if (StartupReport.ExposureWarning(options) is { } warning)
            {
                logger.LogWarning("{Warning}", warning);
            }

            await app.WaitForShutdownAsync(CancellationToken.None);
            logger.LogInformation("Hoard stopped.");
            return 0;
        }
        catch (Exception e)
        {
            // The type only: framework, JSON and crypto messages can carry values or file content.
            await error.WriteLineAsync($"Hoard stopped unexpectedly ({e.GetType().FullName}). No details are shown because they could contain configuration values.");
            return 1;
        }
    }

    private static string Version
    {
        get
        {
            var version = typeof(HoardStartup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            var metadata = version.IndexOf('+', StringComparison.Ordinal);
            return metadata < 0 ? version : version[..metadata];
        }
    }

    private static string BindFailure(HoardOptions options, IOException e)
    {
        var listen = options.Listen;
        var endpoint = $"{(listen.Address.IsLocalhost ? "127.0.0.1" : listen.Address.UrlHost)}:{listen.Port}";
        var sources = string.Join(", ", options.Sources
            .Where(v => v.Option == OptionNames.Address || v.Option == OptionNames.Port)
            .Select(v => $"{v.Option.Key} from {v.Source}"));
        return e.InnerException is AddressInUseException
            ? $"Hoard could not start: {endpoint} is already in use ({sources})."
            : $"Hoard could not start: {endpoint} could not be bound; the address is not available on this machine ({sources}).";
    }

    /// <summary>
    /// What was actually bound. <c>localhost</c> is 127.0.0.1 and, where the machine has it, ::1;
    /// Kestrel carries on without ::1, so a connection to it is the only reliable answer.
    /// </summary>
    private static async Task<IReadOnlyList<string>> BoundAddressesAsync(ListenEndpoint listen)
    {
        if (!listen.Address.IsLocalhost)
        {
            return [$"{listen.Address.UrlHost}:{listen.Port}"];
        }

        var bound = new List<string> { $"127.0.0.1:{listen.Port}" };
        if (Socket.OSSupportsIPv6)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await socket.ConnectAsync(IPAddress.IPv6Loopback, listen.Port, timeout.Token);
                bound.Add($"[::1]:{listen.Port}");
            }
            catch (Exception e) when (e is SocketException or OperationCanceledException)
            {
            }
        }

        return bound;
    }
}
