namespace Hoard.Release.Tests;

/// <summary>eng/release/status-notes.sh extracts the README release-status section as release notes.</summary>
public sealed class StatusNotesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("hoard-notes-").FullName;

    [Fact]
    public void Section_between_markers_is_extracted_without_them()
    {
        Shell.SkipOnWindows();
        var readme = Readme("""
            # Hoard

            <!-- release-status:start -->
            ## Status

            **Working:** a health check.
            <!-- release-status:end -->

            ## Documentation
            """);

        var (result, notes) = Extract(readme);

        Assert.True(result.ExitCode == 0, result.Stderr);
        Assert.Equal("## Status\n\n**Working:** a health check.\n", notes);
    }

    [Theory]
    [InlineData("intro\n<!-- release-status:end -->\n", "release-status:start marker")]
    [InlineData("<!-- release-status:start -->\nWorking\n", "release-status:end marker")]
    [InlineData("<!-- release-status:start -->\n\n  \n<!-- release-status:end -->\n", "is empty")]
    public void Missing_marker_or_empty_section_fails(string content, string message)
    {
        Shell.SkipOnWindows();

        var (result, _) = Extract(Readme(content));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.Stderr);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Readme(string content)
    {
        var path = Path.Combine(_root, "README.md");
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
        return path;
    }

    private (Shell.Result Result, string Notes) Extract(string readme)
    {
        var notes = Path.Combine(_root, "notes.md");
        var result = Shell.Run(
            _root,
            "bash",
            [Path.Combine(Shell.RepositoryRoot, "eng", "release", "status-notes.sh"), readme, notes]);
        return (result, File.Exists(notes) ? File.ReadAllText(notes) : "");
    }
}
