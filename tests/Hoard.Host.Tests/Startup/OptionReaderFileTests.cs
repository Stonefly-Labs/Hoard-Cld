using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Config file rules (research R2, FR-005, FR-008).</summary>
public sealed class OptionReaderFileTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public void Unparseable_json_is_reported_by_line_and_position_only()
    {
        var file = work.Write("hoard.json", "{\n  \"listen\": ");

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Equal($"file {file}", problem.Location);
        Assert.Matches(@"^is not valid JSON \(line \d+, position \d+\)", problem.Message);
        Assert.DoesNotContain("listen", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_commas_are_rejected_and_comments_allowed()
    {
        work.Write("hoard.json", """{ "listen": { "port": 1 }, }""");
        Assert.Single(Options.Read(work.Path).Problems);

        work.Write("hoard.json", "// a comment\n{ /* inline */ \"listen\": { \"port\": 1 } }");
        Assert.Empty(Options.Read(work.Path).Problems);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    public void The_root_must_be_an_object(string content)
    {
        work.Write("hoard.json", content);

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Contains("JSON object", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "data": { "path": ["a"] } }""", "data.path")]
    [InlineData("""{ "data": { "path": null } }""", "data.path")]
    [InlineData("""{ "data": { "path": true } }""", "data.path")]
    [InlineData("""{ "data": { "path": 5 } }""", "data.path")]
    [InlineData("""{ "data": { "path": {} } }""", "data.path")]
    [InlineData("""{ "listen": "x" }""", "listen")]
    [InlineData("""{ "listen": { "tls": { "password": 1234 } } }""", "listen.tls.password")]
    public void Values_of_the_wrong_type_are_problems(string content, string key)
    {
        var file = work.Write("hoard.json", content);

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Equal($"file {file} {key}", problem.Location);
        Assert.DoesNotContain("1234", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keys_that_differ_only_in_case_are_a_problem()
    {
        work.Write("hoard.json", """{ "listen": { "port": 1, "Port": 2 } }""");

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Contains("more than once", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "config": "other.json" }""", "config")]
    [InlineData("""{ "storage": { "path": "x" } }""", "storage")]
    [InlineData("""{ "listen": { "prot": "secretish" } }""", "listen.prot")]
    public void Unknown_keys_are_named_without_their_values(string content, string key)
    {
        var file = work.Write("hoard.json", content);

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Equal($"file {file} {key}", problem.Location);
        Assert.StartsWith("unknown option", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secretish", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "listen": { "port": 9001 } }""")]
    [InlineData("""{ "listen": { "port": "9001" } }""")]
    [InlineData("""{ "LISTEN": { "Port": 9001 } }""")]
    public void The_port_may_be_a_number_or_a_string(string content)
    {
        work.Write("hoard.json", content);

        var result = Options.Read(work.Path);

        Assert.Empty(result.Problems);
        Assert.Equal("9001", Assert.Single(result.Values).Value);
    }

    [Fact]
    public void An_explicit_config_file_on_the_command_line_must_exist()
    {
        var problem = Assert.Single(Options.Read(work.Path, ["--config", "missing.json"]).Problems);

        Assert.Equal("command line --config", problem.Location);
        Assert.Contains(work.Combine("missing.json"), problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_config_file_in_the_environment_must_exist()
    {
        var problem = Assert.Single(Options.Read(work.Path, environment: new() { ["HOARD_CONFIG"] = "missing.json" }).Problems);

        Assert.Equal("environment HOARD_CONFIG", problem.Location);
        Assert.Contains("does not exist", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_config_file_replaces_the_hoard_json_lookup()
    {
        work.Write("hoard.json", """{ "listen": { "port": 1 } }""");
        var other = work.Write("other.json", """{ "listen": { "port": 2 } }""");

        var result = Options.Read(work.Path, ["--config", "other.json"]);

        Assert.Equal(other, result.ConfigFile);
        Assert.Equal("2", Assert.Single(result.Values).Value);
    }

    [Fact]
    public void A_config_file_larger_than_one_mebibyte_is_refused()
    {
        work.Write("hoard.json", "{" + new string(' ', 1024 * 1024) + "}");

        var problem = Assert.Single(Options.Read(work.Path).Problems);

        Assert.Contains("larger than 1 MiB", problem.Message, StringComparison.Ordinal);
    }
}
