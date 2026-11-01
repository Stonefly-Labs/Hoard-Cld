using System.Globalization;
using System.Net;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Process;

/// <summary>Invalid input fails fast: exit 2, one report, nothing opened, nothing created (US2, SC-002).</summary>
public sealed class InvalidConfigurationTests : IDisposable
{
    private readonly TempDirectory work = new();
    private readonly int port = FreePort.Get();

    public void Dispose() => work.Dispose();

    public static TheoryData<string, string[], string?, string?> Cases => new()
    {
        { "unknown option", ["--listen:prot", "9000"], null, "command line --listen:prot: unknown option" },
        { "malformed port", [], "HOARD_LISTEN__PORT=abc", "environment HOARD_LISTEN__PORT: 'abc' is not a port" },
        { "host name", ["--listen:address", "example.com"], null, "command line --listen:address: 'example.com' is not an IP address" },
        { "missing config", ["--config", "missing.json"], null, "command line --config: the config file" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Invalid_input_exits_2_with_the_problem_named(string name, string[] args, string? variable, string? expected)
    {
        _ = name;
        var environment = variable is null ? null : new Dictionary<string, string> { [variable.Split('=')[0]] = variable.Split('=')[1] };

        await AssertRefused(args, environment, expected!);
    }

    [Fact]
    public async Task A_malformed_hoard_json_exits_2()
    {
        work.Write("hoard.json", "{ \"listen\": ");

        await AssertRefused([], null, $"file {work.Combine("hoard.json")}: is not valid JSON");
    }

    [Fact]
    public async Task An_unwritable_data_directory_exits_2()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess, "needs Unix file modes as a non-root user");
        Directory.CreateDirectory(work.Combine("data"));
#pragma warning disable CA1416 // guarded by the skip above
        File.SetUnixFileMode(work.Combine("data"), UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416

        await AssertRefused([], null, "default data.path", expectDataDirectory: true);
    }

    [Fact]
    public async Task Three_problems_are_reported_in_one_run()
    {
        work.Write("state", "a file, not a directory");
        await using var host = HostProcess.Start(work.Path, ["--listen:port", "0", "--listen:address", "10.0.0.300", "--data:path", "state"]);

        Assert.Equal(2, await host.WaitForExitAsync());
        var stderr = host.StandardError;
        Assert.StartsWith("Hoard could not start: 3 problems in its configuration.", stderr, StringComparison.Ordinal);
        Assert.Contains("--listen:port", stderr, StringComparison.Ordinal);
        Assert.Contains("--listen:address", stderr, StringComparison.Ordinal);
        Assert.Contains("command line --data:path: '" + work.Combine("state") + "' is a file", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_port_in_use_exits_1_naming_the_address()
    {
        var text = port.ToString(CultureInfo.InvariantCulture);
        await using var first = HostProcess.Start(work.Path, ["--listen:port", text]);
        Assert.True(await first.WaitForOutputAsync("Config file:"), first.Output);

        await using var second = HostProcess.Start(work.Path, ["--listen:port", text]);

        Assert.Equal(1, await second.WaitForExitAsync());
        Assert.Contains($"Hoard could not start: 127.0.0.1:{port} is already in use (listen.address from default, listen.port from command line --listen:port).",
            second.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", second.Output, StringComparison.Ordinal);
    }

    private async Task AssertRefused(string[] args, Dictionary<string, string>? environment, string expected, bool expectDataDirectory = false)
    {
        string[] withPort = args.Contains("--listen:port") || environment?.ContainsKey("HOARD_LISTEN__PORT") == true ? args : [.. args, "--listen:port", port.ToString(CultureInfo.InvariantCulture)];
        await using var host = HostProcess.Start(work.Path, withPort, environment);

        Assert.Equal(2, await host.WaitForExitAsync());

        var stderr = host.StandardError;
        Assert.StartsWith("Hoard could not start: 1 problem in its configuration.", stderr, StringComparison.Ordinal);
        Assert.Contains(expected, stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", host.Output, StringComparison.Ordinal);
        Assert.Empty(host.StandardOutput);
        Assert.False(FreePort.IsListening(IPAddress.Loopback, port));
        Assert.Equal(expectDataDirectory, Directory.Exists(work.Combine("data")));
    }
}
