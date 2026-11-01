using System.Text.Json;

namespace Hoard.Host.Startup;

/// <summary>Everything read from the three sources, before precedence and validation.</summary>
internal sealed record OptionReadResult(IReadOnlyList<OptionValue> Values, IReadOnlyList<ConfigProblem> Problems, string? ConfigFile);

/// <summary>
/// Reads the command line, <c>HOARD_*</c> environment variables and the JSON config file into
/// <c>(option, value, source)</c> rows (research R2, R3). It never echoes the value of an unknown
/// option, a positional argument or the password: any of them may be a misplaced secret.
/// </summary>
internal static class OptionReader
{
    private const string EnvironmentPrefix = "HOARD_";

    public static OptionReadResult Read(IReadOnlyList<string> args, IReadOnlyDictionary<string, string> environment, string workingDirectory) =>
        Read(args, environment, workingDirectory, OptionNames.All);

    public static OptionReadResult Read(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        IReadOnlyList<OptionDefinition> options)
    {
        var values = new List<OptionValue>();
        var problems = new List<ConfigProblem>();
        ReadCommandLine(args, options, values, problems);
        ReadEnvironment(environment, options, values, problems);

        var config = values.FirstOrDefault(v => v.Option == OptionNames.Config && v.Source.Kind == SourceKind.CommandLine)
            ?? values.FirstOrDefault(v => v.Option == OptionNames.Config);
        values.RemoveAll(v => v.Option == OptionNames.Config);

        var configFile = ReadFile(config, workingDirectory, options, values, problems);
        return new OptionReadResult(values, problems, configFile);
    }

    private static void ReadCommandLine(IReadOnlyList<string> args, IReadOnlyList<OptionDefinition> options, List<OptionValue> values, List<ConfigProblem> problems)
    {
        var known = string.Join(", ", options.Where(o => o.HasCommandLine).Select(o => o.CommandLineName));
        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
            {
                problems.Add(new($"command line argument {i + 1}", "unexpected argument; options have the form --name value or --name=value"));
                continue;
            }

            var equals = token.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? token : token[..equals];
            string? value = equals < 0 ? null : token[(equals + 1)..];

            var option = options.FirstOrDefault(o => string.Equals(o.CommandLineName, name, StringComparison.OrdinalIgnoreCase));
            if (option is { HasCommandLine: false })
            {
                // Its value is consumed, whatever it starts with, so it is never parsed or echoed.
                i += value is null && i + 1 < args.Count ? 1 : 0;
                problems.Add(new(
                    $"command line {option.CommandLineName}",
                    $"the certificate password cannot be given on the command line, where other users can see it; set {option.EnvironmentName} or {option.Key} in the config file"));
                continue;
            }

            if (option is null)
            {
                // Its likely value is consumed too, so one mistake is one problem.
                i += value is null && i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? 1 : 0;
                problems.Add(new($"command line {name}", $"unknown option. Known options: {known}"));
                continue;
            }

            // After a known option the next token is always its value, whatever it starts with.

            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    problems.Add(new($"command line {name}", $"has no value; expected {option.CommandLineName} <value> or {option.CommandLineName}=<value>"));
                    continue;
                }

                value = args[++i];
            }

            if (values.Any(v => v.Option == option && v.Source.Kind == SourceKind.CommandLine))
            {
                problems.Add(new($"command line {name}", "is given more than once; give it once"));
                continue;
            }

            values.Add(new(option, value, new OptionSource(SourceKind.CommandLine, name)));
        }
    }

    private static void ReadEnvironment(IReadOnlyDictionary<string, string> environment, IReadOnlyList<OptionDefinition> options, List<OptionValue> values, List<ConfigProblem> problems)
    {
        var known = string.Join(", ", options.Select(o => o.EnvironmentName));
        foreach (var (name, value) in environment.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!name.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var option = options.FirstOrDefault(o => string.Equals(o.EnvironmentName, name, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                problems.Add(new($"environment {name}", $"unknown option. Known variables: {known}"));
                continue;
            }

            if (values.Any(v => v.Option == option && v.Source.Kind == SourceKind.Environment))
            {
                problems.Add(new($"environment {name}", "is set more than once with different capitalization; set it once"));
                continue;
            }

            values.Add(new(option, value, new OptionSource(SourceKind.Environment, name)));
        }
    }

    /// <summary>Reads the named file, or <c>hoard.json</c> in the working directory if it exists. Returns the path read.</summary>
    private static string? ReadFile(OptionValue? config, string workingDirectory, IReadOnlyList<OptionDefinition> options, List<OptionValue> values, List<ConfigProblem> problems)
    {
        string path;
        if (config is null)
        {
            path = Path.Combine(workingDirectory, "hoard.json");
            if (!File.Exists(path))
            {
                return null;
            }
        }
        else if (config.Value.Length == 0)
        {
            problems.Add(new(config.Source.Location, "is empty; expected the path to a JSON config file"));
            return null;
        }
        else
        {
            path = Path.GetFullPath(config.Value, workingDirectory);
        }

        var outcome = BoundedFile.TryRead(path, out var content);
        switch (outcome)
        {
            case BoundedFile.Outcome.Missing when config is not null:
                problems.Add(new(config.Source.Location, $"the config file {path} does not exist"));
                return null;
            case BoundedFile.Outcome.Empty:
                return null;
            case not BoundedFile.Outcome.Read:
                problems.Add(new($"file {path}", $"could not be read: {BoundedFile.Describe(outcome)}"));
                return null;
        }

        var problemCount = problems.Count;
        var fileValues = new List<OptionValue>();
        ParseFile(path, content, options, fileValues, problems);
        if (fileValues.Count == 0 && problems.Count == problemCount)
        {
            // An empty object is no file at all.
            return null;
        }

        values.AddRange(fileValues);
        return path;
    }

    private static void ParseFile(string path, byte[] content, IReadOnlyList<OptionDefinition> options, List<OptionValue> values, List<ConfigProblem> problems)
    {
        if (content.All(b => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false });
        }
        catch (JsonException e)
        {
            // Hoard's own wording: the parser's message can quote file content.
            problems.Add(new($"file {path}", $"is not valid JSON (line {(e.LineNumber ?? 0) + 1}, position {(e.BytePositionInLine ?? 0) + 1})"));
            return;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problems.Add(new($"file {path}", "must contain a JSON object, such as { \"listen\": { \"port\": 8484 } }"));
                return;
            }

            var fileKeys = options.Where(o => o.IsFileKey).ToList();
            ReadObject(path, document.RootElement, prefix: "", fileKeys, values, problems);
        }
    }

    private static void ReadObject(string path, JsonElement element, string prefix, List<OptionDefinition> keys, List<OptionValue> values, List<ConfigProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix + property.Name;
            var location = $"file {path} {key}";
            if (!seen.Add(property.Name))
            {
                problems.Add(new(location, "appears more than once (keys match ignoring case); keep one"));
                continue;
            }

            var option = keys.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            var isGroup = keys.Any(o => o.Key.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase));
            if (option is null && !isGroup)
            {
                problems.Add(new(location, $"unknown option. Known keys: {string.Join(", ", keys.Select(o => o.Key))}"));
                continue;
            }

            if (isGroup)
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new(location, "must be an object"));
                    continue;
                }

                ReadObject(path, property.Value, key + ".", keys, values, problems);
                continue;
            }

            var portAllowsNumber = option == OptionNames.Port;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    values.Add(new(option!, property.Value.GetString()!, new OptionSource(SourceKind.File, key, path)));
                    break;
                case JsonValueKind.Number when portAllowsNumber:
                    values.Add(new(option!, property.Value.GetRawText(), new OptionSource(SourceKind.File, key, path)));
                    break;
                default:
                    problems.Add(new(location, portAllowsNumber ? "must be a number or a string" : "must be a string"));
                    break;
            }
        }
    }
}
