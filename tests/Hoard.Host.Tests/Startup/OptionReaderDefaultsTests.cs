using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

public sealed class OptionReaderDefaultsTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public void No_input_gives_every_default_from_the_default_source()
    {
        var result = Options.Validate(work.Path);

        Assert.Empty(result.Problems);
        var options = result.Options!;
        Assert.True(options.Listen.Address.IsLocalhost);
        Assert.Equal(8484, options.Listen.Port);
        Assert.Equal(TlsMode.None, options.Tls.Mode);
        Assert.Equal(Path.Combine(work.Path, "data"), options.DataDirectory.Path);
        Assert.Null(options.ConfigFile);
        Assert.All(options.Sources, s => Assert.Equal(SourceKind.Default, s.Source.Kind));
        Assert.Equal(["listen.address", "listen.port", "data.path"], options.Sources.Select(s => s.Option.Key));
    }

    [Fact]
    public void Framework_and_other_environment_variables_are_ignored()
    {
        var result = Options.Read(work.Path, environment: new()
        {
            ["ASPNETCORE_URLS"] = "http://0.0.0.0:1",
            ["DOTNET_URLS"] = "http://0.0.0.0:2",
            ["Kestrel__Endpoints__X__Url"] = "http://0.0.0.0:3",
            ["PATH"] = "/usr/bin",
            ["HOARDING"] = "not ours: no underscore after HOARD",
        });

        Assert.Empty(result.Problems);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void Urls_is_an_unknown_command_line_option()
    {
        var result = Options.Read(work.Path, ["--urls", "http://0.0.0.0:1"]);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("command line --urls", problem.Location);
        Assert.Contains("unknown option", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0", problem.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_hoard_json_in_the_working_directory_is_read_and_named()
    {
        var file = work.Write("hoard.json", """{ "listen": { "port": 9001 } }""");

        var result = Options.Read(work.Path);

        Assert.Empty(result.Problems);
        Assert.Equal(file, result.ConfigFile);
        var value = Assert.Single(result.Values);
        Assert.Equal("listen.port", value.Option.Key);
        Assert.Equal("9001", value.Value);
        Assert.Equal($"file {file}", value.Source.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("{}")]
    [InlineData("{ }")]
    public void An_empty_file_equals_no_file(string content)
    {
        work.Write("hoard.json", content);

        var result = Options.Read(work.Path);

        Assert.Empty(result.Problems);
        Assert.Empty(result.Values);
        Assert.Null(result.ConfigFile);
    }
}
