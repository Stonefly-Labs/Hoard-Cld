using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Environment reading rules (research R3).</summary>
public sealed class OptionReaderEnvironmentTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public void An_unknown_hoard_variable_is_named_without_its_value()
    {
        var problem = Assert.Single(Options.Read(work.Path, environment: new() { ["HOARD_LISTEN__PROT"] = "secretish" }).Problems);

        Assert.Equal("environment HOARD_LISTEN__PROT", problem.Location);
        Assert.StartsWith("unknown option. Known variables: HOARD_CONFIG, HOARD_LISTEN__ADDRESS", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secretish", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_match_ignoring_case_and_keep_their_spelling()
    {
        var value = Assert.Single(Options.Read(work.Path, environment: new() { ["hoard_listen__port"] = "9000" }).Values);

        Assert.Equal("listen.port", value.Option.Key);
        Assert.Equal("environment hoard_listen__port", value.Source.ToString());
    }

    [Fact]
    public void An_empty_value_is_malformed_not_the_default()
    {
        var result = Options.Validate(work.Path, environment: new() { ["HOARD_DATA__PATH"] = "" });

        var problem = Assert.Single(result.Problems);
        Assert.Equal("environment HOARD_DATA__PATH", problem.Location);
        Assert.Contains("is empty", problem.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(work.Combine("data")));
    }

    [Fact]
    public void The_password_is_read_from_the_environment()
    {
        var value = Assert.Single(Options.Read(work.Path, environment: new() { ["HOARD_LISTEN__TLS__PASSWORD"] = "pw" }).Values);

        Assert.Equal("listen.tls.password", value.Option.Key);
    }
}
