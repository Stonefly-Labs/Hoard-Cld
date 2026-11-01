using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Hoard.Host.Startup;

/// <summary>
/// The listen address: <c>localhost</c> (<see cref="Ip"/> is null) or one IP address written in
/// canonical form (research R4). Host names are never accepted, so exposure never depends on DNS.
/// </summary>
internal sealed record ListenAddress(string Text, IPAddress? Ip, bool IsLoopback)
{
    private const string ExpectedForm = "expected 'localhost' or an IPv4 or IPv6 address written in full, such as 127.0.0.1, 0.0.0.0 or ::1 (host names are not accepted)";

    public static readonly ListenAddress Localhost = new("localhost", null, IsLoopback: true);

    public bool IsLocalhost => Ip is null;

    public static bool TryParse(string value, [NotNullWhen(true)] out ListenAddress? address, [NotNullWhen(false)] out string? error)
    {
        address = null;
        error = null;
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            address = Localhost;
            return true;
        }

        if (value.Length == 0)
        {
            error = $"is empty; {ExpectedForm}";
            return false;
        }

        if (!IPAddress.TryParse(value, out var ip))
        {
            error = $"'{value}' is not an IP address; {ExpectedForm}";
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.ScopeId != 0)
        {
            error = $"'{value}' has a scope; {ExpectedForm}";
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            error = $"'{value}' is an IPv4-mapped IPv6 address; use the IPv4 form ({ip.MapToIPv4()})";
            return false;
        }

        // The round-trip rejects short, integer, octal, hex, bracketed, scoped and padded forms.
        var canonical = ip.ToString();
        var comparison = ip.AddressFamily == AddressFamily.InterNetworkV6 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(canonical, value, comparison))
        {
            error = $"'{value}' is not written in canonical form; {ExpectedForm}";
            return false;
        }

        var loopback = ip.AddressFamily == AddressFamily.InterNetwork
            ? ip.GetAddressBytes()[0] == 127
            : ip.Equals(IPAddress.IPv6Loopback);
        address = new ListenAddress(canonical, ip, loopback);
        return true;
    }

    /// <summary>The address as it appears in a URL: IPv6 in brackets.</summary>
    public string UrlHost => Ip?.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{Text}]" : Text;
}
