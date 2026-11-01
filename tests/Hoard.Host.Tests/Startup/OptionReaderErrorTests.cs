using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Command-line reading rules (research R2).</summary>
public sealed class OptionReaderErrorTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Theory]
    [InlineData("--listen:port", "9000")]
    [InlineData("--listen:port=9000")]
    [InlineData("--LISTEN:PORT", "9000")]
    public void Both_forms_and_any_case_are_read(params string[] args)
    {
        var result = Options.Read(work.Path, args);

        Assert.Empty(result.Problems);
        var value = Assert.Single(result.Values);
        Assert.Equal("listen.port", value.Option.Key);
        Assert.Equal("9000", value.Value);
    }

    [Fact]
    public void An_equals_sign_splits_only_once()
    {
        var value = Assert.Single(Options.Read(work.Path, ["--data:path=a=b"]).Values);

        Assert.Equal("a=b", value.Value);
    }

    [Fact]
    public void An_unknown_option_is_named_without_its_value()
    {
        var result = Options.Read(work.Path, ["--listen:prot", "9000-secretish"]);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("command line --listen:prot", problem.Location);
        Assert.StartsWith("unknown option. Known options: --config, --listen:address, --listen:port", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--listen:tls:password", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secretish", problem.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_option_with_an_inline_value_is_named_without_it()
    {
        var problem = Assert.Single(Options.Read(work.Path, ["--nope=secretish"]).Problems);

        Assert.Equal("command line --nope", problem.Location);
        Assert.DoesNotContain("secretish", problem.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secretish")]
    [InlineData("-x")]
    [InlineData("/x")]
    [InlineData("--")]
    [InlineData("-")]
    public void Positional_arguments_and_switches_are_reported_by_position(string token)
    {
        var result = Options.Read(work.Path, ["--listen:port", "9000", token]);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("command line argument 3", problem.Location);
        Assert.Equal("unexpected argument; options have the form --name value or --name=value", problem.Message);
    }

    [Fact]
    public void A_known_option_without_a_value_is_a_problem()
    {
        var problem = Assert.Single(Options.Read(work.Path, ["--listen:port"]).Problems);

        Assert.Equal("command line --listen:port", problem.Location);
        Assert.Contains("has no value", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_option_twice_is_a_problem()
    {
        var problem = Assert.Single(Options.Read(work.Path, ["--listen:port", "1", "--listen:port=2"]).Problems);

        Assert.Equal("command line --listen:port", problem.Location);
        Assert.Contains("more than once", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--listen:address")]
    [InlineData("-listen")]
    [InlineData("/listen")]
    public void After_a_known_option_the_next_token_is_its_value(string next)
    {
        var result = Options.Read(work.Path, ["--data:path", next]);

        Assert.Empty(result.Problems);
        Assert.Equal(next, Assert.Single(result.Values).Value);
    }

    [Theory]
    [InlineData("--listen:tls:password", "--s3cret")]
    [InlineData("--listen:tls:password=--s3cret")]
    [InlineData("--LISTEN:TLS:PASSWORD", "s3cret")]
    public void The_password_is_refused_on_the_command_line_without_echoing_it(params string[] args)
    {
        var result = Options.Read(work.Path, args);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("command line --listen:tls:password", problem.Location);
        Assert.Contains("HOARD_LISTEN__TLS__PASSWORD", problem.Message, StringComparison.Ordinal);
        Assert.Contains("config file", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", problem.ToString(), StringComparison.Ordinal);
        Assert.Empty(result.Values);
    }
}
