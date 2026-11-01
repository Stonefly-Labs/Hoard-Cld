using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Per-option rules and collecting every problem in one pass (FR-006, data-model rules 1–4).</summary>
public sealed class OptionValidatorTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("+80")]
    [InlineData(" 80")]
    [InlineData("80 ")]
    [InlineData("abc")]
    [InlineData("8484.0")]
    [InlineData("１２３")]
    [InlineData("")]
    public void Malformed_ports_are_problems(string port)
    {
        var problem = Assert.Single(Options.Validate(work.Path, [$"--listen:port={port}"]).Problems);

        Assert.Equal("command line --listen:port", problem.Location);
        Assert.Contains("integer 1–65535", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("65535")]
    public void Ports_in_range_are_accepted(string port)
    {
        Assert.Empty(Options.Validate(work.Path, ["--listen:port", port]).Problems);
    }

    [Fact]
    public void An_empty_data_path_is_malformed()
    {
        var problem = Assert.Single(Options.Validate(work.Path, ["--data:path="]).Problems);

        Assert.Contains("is empty", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_from_every_source_are_reported_together_and_nothing_is_created()
    {
        work.Write("hoard.json", """{ "listen": { "address": "10.0.0.300" } }""");

        var result = Options.Validate(work.Path, ["--listen:port", "0"], new() { ["HOARD_LISTEN__TLS__MODE"] = "tls" });

        Assert.Null(result.Options);
        Assert.Equal(3, result.Problems.Count);
        Assert.Contains(result.Problems, p => p.Location == "command line --listen:port");
        Assert.Contains(result.Problems, p => p.Location == "environment HOARD_LISTEN__TLS__MODE");
        Assert.Contains(result.Problems, p => p.Location == $"file {work.Combine("hoard.json")} listen.address");
        Assert.False(Directory.Exists(work.Combine("data")));
    }

    [Fact]
    public void A_data_path_that_is_a_file_is_reported_with_the_other_problems()
    {
        work.Write("data", "not a directory");

        var result = Options.Validate(work.Path, ["--listen:port", "0"]);

        Assert.Equal(2, result.Problems.Count);
        var problem = Assert.Single(result.Problems, p => p.Location == "default data.path");
        Assert.Contains("is a file", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reading_problems_stop_the_data_directory_being_created()
    {
        var result = Options.Validate(work.Path, ["--nope", "x"]);

        Assert.Single(result.Problems);
        Assert.False(Directory.Exists(work.Combine("data")));
    }

    [Fact]
    public void A_clean_configuration_creates_the_data_directory()
    {
        var result = Options.Validate(work.Path, ["--data:path", "state"]);

        Assert.Empty(result.Problems);
        Assert.True(result.Options!.DataDirectory.Created);
        Assert.True(Directory.Exists(work.Combine("state")));
    }
}
