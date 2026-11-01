namespace Hoard.Host.Startup;

internal enum SourceKind
{
    Default,
    File,
    Environment,
    CommandLine,
}

/// <summary>
/// Where a value came from. <see cref="Name"/> is the option's name as that source spelled it
/// (the variable, the switch or the key path); <see cref="FilePath"/> is set for the file.
/// </summary>
internal sealed record OptionSource(SourceKind Kind, string Name, string? FilePath = null)
{
    public static OptionSource Default(OptionDefinition option) => new(SourceKind.Default, option.Key);

    /// <summary>The source as the summary shows it: <c>default</c>, <c>file /x/hoard.json</c>, ...</summary>
    public override string ToString() => Kind switch
    {
        SourceKind.Default => "default",
        SourceKind.File => $"file {FilePath}",
        SourceKind.Environment => $"environment {Name}",
        _ => $"command line {Name}",
    };

    /// <summary>The source and the option's name in that source's form, as problems name them.</summary>
    public string Location => Kind switch
    {
        SourceKind.Default => $"default {Name}",
        SourceKind.File => $"file {FilePath} {Name}",
        _ => ToString(),
    };
}

/// <summary>One raw value read from one source.</summary>
internal sealed record OptionValue(OptionDefinition Option, string Value, OptionSource Source);

/// <summary>
/// One startup problem: where (source and name, or a position) and what is wrong with the fix.
/// It never holds the value of an unknown or secret option.
/// </summary>
internal sealed record ConfigProblem(string Location, string Message)
{
    public override string ToString() => $"{Location}: {Message}";
}
