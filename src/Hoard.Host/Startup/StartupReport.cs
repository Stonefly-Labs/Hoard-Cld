using System.Globalization;
using System.Text;

namespace Hoard.Host.Startup;

/// <summary>
/// The operator-facing text: the stderr problem report, the startup summary and the exposure
/// warning (contracts/configuration-and-startup.md). The password only ever renders as <c>(set)</c>.
/// </summary>
internal static class StartupReport
{
    public static string Problems(IReadOnlyList<ConfigProblem> problems)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Hoard could not start: {problems.Count} {(problems.Count == 1 ? "problem" : "problems")} in its configuration.\n");
        foreach (var problem in problems)
        {
            text.Append(CultureInfo.InvariantCulture, $"  - {problem}\n");
        }

        return text.ToString();
    }

    /// <param name="bound">The addresses actually bound, such as <c>127.0.0.1:8484</c> and <c>[::1]:8484</c>.</param>
    public static string Summary(HoardOptions options, IReadOnlyList<string> bound, string version)
    {
        var listen = options.Listen;
        var details = new List<string>();
        switch (options.Tls.Mode)
        {
            case TlsMode.Proxy:
                details.Add("TLS mode proxy");
                break;
            case TlsMode.Https:
                var certificate = options.Tls.Certificate!;
                details.Add(string.Create(CultureInfo.InvariantCulture,
                    $"TLS mode https, certificate {certificate.Subject}, expires {certificate.NotAfter.ToUniversalTime():yyyy-MM-dd}"));
                break;
        }

        if (listen.Address.IsLocalhost)
        {
            details.Add("bound: " + string.Join(", ", bound));
        }

        var scheme = options.Tls.Mode == TlsMode.Https ? "https" : "http";
        var url = string.Create(CultureInfo.InvariantCulture, $"{scheme}://{listen.Address.UrlHost}:{listen.Port}");
        var configName = options.ConfigFile is null ? null : Path.GetFileName(options.ConfigFile);
        var values = options.Sources.Select(v =>
        {
            var value = v.Option.IsSecret ? "(set)" : v.Value;
            var overrides = options.Overridden.Contains(v.Option) ? $", overrides {configName}" : "";
            return $"{v.Option.Key}={value} ({v.Source}{overrides})";
        });

        return $"""
            Hoard {version} started.
              Listening:      {url}{(details.Count > 0 ? $" ({string.Join(", ", details)})" : "")}
              Data directory: {options.DataDirectory.Path} ({(options.DataDirectory.Created ? "created" : "existing")})
              Options:        {string.Join(", ", values)}
              Config file:    {options.ConfigFile ?? "none"}
            """.ReplaceLineEndings("\n");
    }

    /// <summary>The warning for a listener reachable from the network; null on loopback (FR-010).</summary>
    public static string? ExposureWarning(HoardOptions options)
    {
        if (options.Listen.IsLoopback)
        {
            return null;
        }

        var warning = $"Hoard is listening on {options.Listen.Address.Text}, which is reachable from the network. "
            + "Package reads are anonymous: anyone who can reach this address can use the cache.";
        return options.Tls.Mode == TlsMode.Proxy
            ? warning + " Hoard serves plain HTTP here and relies on a TLS-terminating reverse proxy in front of it."
            : warning;
    }
}
