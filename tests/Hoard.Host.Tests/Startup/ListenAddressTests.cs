using System.Net;
using Hoard.Host.Startup;

namespace Hoard.Host.Tests.Startup;

/// <summary>Address parsing and loopback classification (FR-011, research R4).</summary>
public sealed class ListenAddressTests
{
    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LocalHost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.10.20.30", true)]
    [InlineData("::1", true)]
    [InlineData("10.1.2.3", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("FE80::1", false)]
    [InlineData("128.0.0.1", false)]
    public void Accepts_localhost_and_canonical_addresses(string value, bool loopback)
    {
        Assert.True(ListenAddress.TryParse(value, out var address, out var error), error);

        Assert.Equal(loopback, address!.IsLoopback);
        Assert.Equal(value, address.Text, ignoreCase: true);
    }

    [Fact]
    public void All_interfaces_map_to_the_any_addresses()
    {
        Assert.True(ListenAddress.TryParse("0.0.0.0", out var v4, out _));
        Assert.True(ListenAddress.TryParse("::", out var v6, out _));
        Assert.True(ListenAddress.TryParse("localhost", out var localhost, out _));

        Assert.Equal(IPAddress.Any, v4!.Ip);
        Assert.Equal(IPAddress.IPv6Any, v6!.Ip);
        Assert.True(localhost!.IsLocalhost);
        Assert.Null(localhost.Ip);
    }

    [Theory]
    [InlineData("")]
    [InlineData("127.1")]
    [InlineData("2130706433")]
    [InlineData("010.0.0.1")]
    [InlineData("0x7f.0.0.1")]
    [InlineData(" 127.0.0.1")]
    [InlineData("127.0.0.1 ")]
    [InlineData("[::1]")]
    [InlineData("fe80::1%eth0")]
    [InlineData("fe80::1%1")]
    [InlineData("10.0.0.300")]
    [InlineData("example.com")]
    [InlineData("0:0:0:0:0:0:0:1")]
    public void Rejects_anything_else_with_the_expected_form(string value)
    {
        Assert.False(ListenAddress.TryParse(value, out _, out var error));

        Assert.Contains("localhost", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    public void Rejects_ipv4_mapped_addresses_asking_for_the_ipv4_form(string value)
    {
        Assert.False(ListenAddress.TryParse(value, out _, out var error));

        Assert.Contains("use the IPv4 form", error, StringComparison.Ordinal);
    }
}
