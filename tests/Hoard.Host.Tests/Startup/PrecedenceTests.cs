using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Command line &gt; environment &gt; file &gt; default (FR-001, US3 sc.5, SC-003).</summary>
public sealed class PrecedenceTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    public static TheoryData<string, string, string, string> Values => new()
    {
        { "listen.port", "9001", "9002", "9003" },
        { "listen.address", "127.0.0.2", "127.0.0.3", "127.0.0.4" },
        { "listen.tls.mode", "proxy", "PROXY", "Proxy" },
        { "data.path", "file-data", "env-data", "cli-data" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void All_three_sources_set_the_command_line_wins(string key, string file, string environment, string commandLine)
    {
        var option = OptionNames.All.Single(o => o.Key == key);
        WriteFile(key, file);

        var options = Options.Validate(work.Path, [option.CommandLineName, commandLine], new() { [option.EnvironmentName] = environment }).Options!;

        var effective = options.Sources.Single(s => s.Option == option);
        Assert.Equal(commandLine, effective.Value);
        Assert.Equal(SourceKind.CommandLine, effective.Source.Kind);
        Assert.Contains(option, options.Overridden);
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void Environment_and_file_set_the_environment_wins(string key, string file, string environment, string commandLine)
    {
        _ = commandLine;
        var option = OptionNames.All.Single(o => o.Key == key);
        WriteFile(key, file);

        var options = Options.Validate(work.Path, environment: new() { [option.EnvironmentName] = environment }).Options!;

        var effective = options.Sources.Single(s => s.Option == option);
        Assert.Equal(environment, effective.Value);
        Assert.Equal(SourceKind.Environment, effective.Source.Kind);
        Assert.Contains(option, options.Overridden);
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void Command_line_and_environment_set_the_command_line_wins(string key, string file, string environment, string commandLine)
    {
        _ = file;
        var option = OptionNames.All.Single(o => o.Key == key);

        var options = Options.Validate(work.Path, [option.CommandLineName, commandLine], new() { [option.EnvironmentName] = environment }).Options!;

        var effective = options.Sources.Single(s => s.Option == option);
        Assert.Equal(commandLine, effective.Value);
        Assert.DoesNotContain(option, options.Overridden);
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void Only_the_file_set_the_file_is_used(string key, string file, string environment, string commandLine)
    {
        _ = (environment, commandLine);
        var option = OptionNames.All.Single(o => o.Key == key);
        WriteFile(key, file);

        var options = Options.Validate(work.Path).Options!;

        var effective = options.Sources.Single(s => s.Option == option);
        Assert.Equal(file, effective.Value);
        Assert.Equal(SourceKind.File, effective.Source.Kind);
        Assert.Empty(options.Overridden);
    }

    [Fact]
    public void The_config_file_location_on_the_command_line_wins_over_the_environment()
    {
        work.Write("hoard.json", """{ "listen": { "port": 1 } }""");
        work.Write("env.json", """{ "listen": { "port": 2 } }""");
        var cli = work.Write("cli.json", """{ "listen": { "port": 3 } }""");

        var options = Options.Validate(work.Path, ["--config", "cli.json"], new() { ["HOARD_CONFIG"] = "env.json" }).Options!;

        Assert.Equal(cli, options.ConfigFile);
        Assert.Equal(3, options.Listen.Port);
    }

    [Fact]
    public void The_summary_names_the_effective_source_and_marks_the_override()
    {
        WriteFile("listen.port", "9001");
        var options = Options.Validate(work.Path, environment: new() { ["HOARD_LISTEN__PORT"] = "9002" }).Options!;

        var summary = StartupReport.Summary(options, ["127.0.0.1:9002"], "1.2.3");

        Assert.Contains("listen.port=9002 (environment HOARD_LISTEN__PORT, overrides hoard.json)", summary, StringComparison.Ordinal);
        Assert.Contains($"Config file:    {work.Combine("hoard.json")}", summary, StringComparison.Ordinal);
    }

    private void WriteFile(string key, string value)
    {
        var parts = key.Split('.');
        var json = $"\"{value}\"";
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            json = $"{{ \"{parts[i]}\": {json} }}";
        }

        work.Write("hoard.json", json);
    }
}
