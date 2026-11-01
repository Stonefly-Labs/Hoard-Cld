namespace Hoard.Host.Startup;

/// <summary>
/// One Hoard option: its file key path and the environment and command-line names derived from it
/// (research R3). Names match ignoring case in every source.
/// </summary>
internal sealed record OptionDefinition(string Key, string? Default, bool IsSecret = false, bool HasCommandLine = true, bool IsFileKey = true)
{
    public string EnvironmentName { get; } = "HOARD_" + Key.Replace(".", "__").ToUpperInvariant();

    public string CommandLineName { get; } = "--" + Key.Replace('.', ':');
}

/// <summary>The complete set of options in this spec (data-model.md).</summary>
internal static class OptionNames
{
    public static readonly OptionDefinition Address = new("listen.address", "localhost");
    public static readonly OptionDefinition Port = new("listen.port", "8484");
    public static readonly OptionDefinition TlsMode = new("listen.tls.mode", null);
    public static readonly OptionDefinition CertificateFile = new("listen.tls.certificateFile", null);
    public static readonly OptionDefinition KeyFile = new("listen.tls.keyFile", null);
    public static readonly OptionDefinition Password = new("listen.tls.password", null, IsSecret: true, HasCommandLine: false);
    public static readonly OptionDefinition DataPath = new("data.path", "./data");

    /// <summary>The config file location: <c>--config</c> / <c>HOARD_CONFIG</c>, never a file key.</summary>
    public static readonly OptionDefinition Config = new("config", null, IsFileKey: false);

    public static readonly IReadOnlyList<OptionDefinition> All =
        [Config, Address, Port, TlsMode, CertificateFile, KeyFile, Password, DataPath];
}
