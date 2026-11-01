using System.Security.Cryptography.X509Certificates;

namespace Hoard.Host.Startup;

internal enum TlsMode
{
    None,
    Proxy,
    Https,
}

/// <summary>The single listener: <c>localhost</c> or one IP address, and a port.</summary>
internal sealed record ListenEndpoint(ListenAddress Address, int Port)
{
    public bool IsLoopback => Address.IsLoopback;
}

/// <summary>The listener's TLS mode; <see cref="Certificate"/> is set only in <see cref="TlsMode.Https"/>.</summary>
internal sealed record TlsSettings(TlsMode Mode, X509Certificate2? Certificate = null);

/// <summary>
/// Validated options (data-model.md): the host is built only from this. <see cref="Sources"/> holds
/// each effective value with its source for the summary, and <see cref="Overridden"/> the options
/// whose file value lost to the environment or command line.
/// </summary>
internal sealed record HoardOptions(
    ListenEndpoint Listen,
    TlsSettings Tls,
    DataDirectory DataDirectory,
    IReadOnlyList<OptionValue> Sources,
    IReadOnlySet<OptionDefinition> Overridden,
    string? ConfigFile);
