using Hoard.Host.Startup;

namespace Hoard.Host.Tests.Support;

/// <summary>Reads and validates options the way the host does, for tests that need a <see cref="HoardOptions"/>.</summary>
internal static class Options
{
    public static OptionReadResult Read(string workingDirectory, string[]? args = null, Dictionary<string, string>? environment = null) =>
        OptionReader.Read(args ?? [], environment ?? [], workingDirectory);

    public static ValidationResult Validate(string workingDirectory, string[]? args = null, Dictionary<string, string>? environment = null) =>
        OptionValidator.Validate(Read(workingDirectory, args, environment), workingDirectory, DateTimeOffset.UtcNow);

    public static HoardOptions Valid(string workingDirectory, params string[] args)
    {
        var result = Validate(workingDirectory, args);
        Assert.True(result.Problems.Count == 0, string.Join(Environment.NewLine, result.Problems));
        return result.Options!;
    }

    /// <summary>Options for an in-process app on a free loopback port with its data directory under <paramref name="workingDirectory"/>.</summary>
    public static HoardOptions Loopback(string workingDirectory, out int port, params string[] args)
    {
        port = FreePort.Get();
        return Valid(workingDirectory, ["--listen:port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), .. args]);
    }
}
