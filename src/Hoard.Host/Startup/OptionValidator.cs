using System.Globalization;

namespace Hoard.Host.Startup;

/// <summary>Either validated options or every problem found.</summary>
internal sealed record ValidationResult(HoardOptions? Options, IReadOnlyList<ConfigProblem> Problems);

/// <summary>
/// Applies precedence (command line &gt; environment &gt; file &gt; default) and the data-model rules,
/// collecting every problem in one pass (FR-006). Preconditions run only when the options they need
/// are valid, and the data directory is created only when nothing else is wrong.
/// </summary>
internal static class OptionValidator
{
    public static ValidationResult Validate(OptionReadResult read, string workingDirectory, DateTimeOffset now)
    {
        var problems = new List<ConfigProblem>(read.Problems);
        var effective = new Dictionary<OptionDefinition, OptionValue>();
        var overridden = new HashSet<OptionDefinition>();
        foreach (var option in OptionNames.All.Where(o => o != OptionNames.Config))
        {
            var values = read.Values.Where(v => v.Option == option).OrderByDescending(v => v.Source.Kind).ToList();
            if (values.Count > 0)
            {
                effective[option] = values[0];
                if (values[0].Source.Kind != SourceKind.File && values.Any(v => v.Source.Kind == SourceKind.File))
                {
                    overridden.Add(option);
                }
            }
            else if (option.Default is not null)
            {
                effective[option] = new(option, option.Default, OptionSource.Default(option));
            }
        }

        var address = effective[OptionNames.Address];
        var listen = ListenAddress.TryParse(address.Value, out var parsed, out var addressError) ? parsed : null;
        if (listen is null)
        {
            problems.Add(new(address.Source.Location, addressError!));
        }

        var portValue = effective[OptionNames.Port];
        var port = ParsePort(portValue.Value);
        if (port is null)
        {
            problems.Add(new(portValue.Source.Location, portValue.Value.Length == 0
                ? "is empty; expected an integer 1–65535"
                : $"'{portValue.Value}' is not a port; expected an integer 1–65535"));
        }

        var modeValue = effective.GetValueOrDefault(OptionNames.TlsMode);
        TlsMode? mode = modeValue?.Value.ToUpperInvariant() switch
        {
            null => TlsMode.None,
            "HTTPS" => TlsMode.Https,
            "PROXY" => TlsMode.Proxy,
            _ => null,
        };
        if (mode is null)
        {
            problems.Add(new(modeValue!.Source.Location, modeValue.Value.Length == 0
                ? "is empty; expected 'https' or 'proxy'"
                : $"'{modeValue.Value}' is not a TLS mode; expected 'https' or 'proxy'"));
        }

        var certificate = effective.GetValueOrDefault(OptionNames.CertificateFile);
        var key = effective.GetValueOrDefault(OptionNames.KeyFile);
        var password = effective.GetValueOrDefault(OptionNames.Password);
        foreach (var value in new[] { certificate, key, password })
        {
            if (value is not null && value.Value.Length == 0)
            {
                problems.Add(new(value.Source.Location, value.Option.IsSecret ? "is empty" : "is empty; expected a file path"));
            }
        }

        var dataValue = effective[OptionNames.DataPath];
        var dataPath = dataValue.Value.Length == 0 ? null : DataDirectory.Resolve(dataValue.Value, workingDirectory);
        if (dataPath is null)
        {
            problems.Add(new(dataValue.Source.Location, "is empty; expected a directory path"));
        }

        // Cross-option rules (FR-027, FR-029).
        if (listen is { IsLoopback: false } && mode == TlsMode.None)
        {
            problems.Add(new(address.Source.Location,
                $"'{listen.Text}' is not loopback, so a TLS mode is required. Set {OptionNames.TlsMode.EnvironmentName} (or {OptionNames.TlsMode.CommandLineName}) "
                + "to 'https' with a certificate, or to 'proxy' if a TLS-terminating reverse proxy is in front."));
        }

        if (mode is not null and not TlsMode.Https)
        {
            foreach (var value in new[] { certificate, key, password }.OfType<OptionValue>())
            {
                problems.Add(new(value.Source.Location, $"is set, but {OptionNames.TlsMode.Key} is not 'https'; remove it, or set the TLS mode to 'https'"));
            }
        }

        if (mode == TlsMode.Https && certificate is null)
        {
            problems.Add(new(modeValue!.Source.Location,
                $"is 'https' but no certificate is configured; set {OptionNames.CertificateFile.Key} ({OptionNames.CertificateFile.EnvironmentName} or {OptionNames.CertificateFile.CommandLineName})"));
        }

        // Preconditions.
        if (dataPath is not null && File.Exists(dataPath))
        {
            problems.Add(new(dataValue.Source.Location, $"'{dataPath}' is a file, not a directory"));
        }

        var loaded = mode == TlsMode.Https && certificate is { Value.Length: > 0 } && key is not { Value.Length: 0 } && password is not { Value.Length: 0 }
            ? CertificateLoader.Load(certificate, key, password, workingDirectory, now, problems)
            : null;

        if (problems.Count > 0)
        {
            loaded?.Dispose();
            return new(null, problems);
        }

        var directory = CreateDataDirectory(dataPath!, dataValue, problems);
        if (directory is null)
        {
            loaded?.Dispose();
            return new(null, problems);
        }

        var sources = OptionNames.All.Where(effective.ContainsKey).Select(o => effective[o]).ToList();
        return new(
            new HoardOptions(
                new ListenEndpoint(listen!, port!.Value),
                new TlsSettings(mode!.Value, loaded),
                directory,
                sources,
                overridden,
                read.ConfigFile),
            problems);
    }

    /// <summary>Decimal digits only, 1–65535: no sign, whitespace or other numerals.</summary>
    private static int? ParsePort(string value) =>
        value.Length is > 0 and <= 5
        && value.All(char.IsAsciiDigit)
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
        && port is >= 1 and <= 65535
            ? port
            : null;

    private static DataDirectory? CreateDataDirectory(string path, OptionValue source, List<ConfigProblem> problems)
    {
        DataDirectory directory;
        try
        {
            directory = DataDirectory.Create(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var reason = e is UnauthorizedAccessException ? "permission denied" : "a parent is missing or not a directory, or the disk refused it";
            problems.Add(new(source.Source.Location, $"'{path}' could not be created ({reason}); choose a directory Hoard can create"));
            return null;
        }

        try
        {
            directory.WriteCheck();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add(new(source.Source.Location, $"'{path}' is not writable; Hoard needs to create files there"));
            return null;
        }

        return directory;
    }
}
