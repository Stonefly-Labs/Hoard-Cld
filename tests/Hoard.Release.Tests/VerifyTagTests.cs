namespace Hoard.Release.Tests;

/// <summary>
/// eng/release/verify-tag.sh against throwaway git repositories: a bare "origin" and a clone
/// with a merge commit on main and an unmerged side branch.
/// </summary>
public sealed class VerifyTagTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("hoard-tag-").FullName;
    private readonly string _clone;
    private readonly string _mergeCommit;
    private readonly string _plainMainCommit;
    private readonly string _sideCommit;

    public VerifyTagTests()
    {
        var origin = Path.Combine(_root, "origin.git");
        _clone = Path.Combine(_root, "clone");
        Shell.Git(_root, "init", "--bare", "--initial-branch=main", origin);
        Shell.Git(_root, "clone", origin, _clone);

        Commit("initial");
        _plainMainCommit = Shell.Git(_clone, "rev-parse", "HEAD");

        Shell.Git(_clone, "switch", "-c", "feature");
        Commit("feature");
        Shell.Git(_clone, "switch", "main");
        Shell.Git(_clone, "merge", "--no-ff", "-m", "Merge feature", "feature");
        _mergeCommit = Shell.Git(_clone, "rev-parse", "HEAD");
        Shell.Git(_clone, "push", "origin", "main");

        Shell.Git(_clone, "switch", "-c", "side");
        Commit("side");
        _sideCommit = Shell.Git(_clone, "rev-parse", "HEAD");
        Shell.Git(_clone, "push", "origin", "side");
        Shell.Git(_clone, "switch", "main");
    }

    [Fact]
    public void Annotated_tag_on_main_merge_commit_is_accepted()
    {
        Shell.SkipOnWindows();
        PushAnnotatedTag("v0.0.1-alpha", _mergeCommit);

        var (result, output) = Verify("v0.0.1-alpha", _mergeCommit);

        Assert.True(result.ExitCode == 0, result.Stderr);
        Assert.Equal("version=0.0.1-alpha", output);
    }

    [Fact]
    public void Annotated_tag_on_non_merge_commit_reachable_from_main_is_accepted()
    {
        Shell.SkipOnWindows();
        PushAnnotatedTag("v0.0.12-alpha", _plainMainCommit);

        var (result, output) = Verify("v0.0.12-alpha", _plainMainCommit);

        Assert.True(result.ExitCode == 0, result.Stderr);
        Assert.Equal("version=0.0.12-alpha", output);
    }

    [Theory]
    [InlineData("v0.0.0-alpha")]
    [InlineData("v0.1.0-alpha")]
    [InlineData("v0.0.1-beta")]
    [InlineData("v0.0.01-alpha")]
    [InlineData("0.0.1-alpha")]
    public void Tag_not_matching_the_alpha_pattern_is_refused(string tag)
    {
        Shell.SkipOnWindows();

        var (result, output) = Verify(tag, _mergeCommit);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release tags must match v0.0.N-alpha", result.Stderr);
        Assert.Empty(output);
    }

    [Fact]
    public void Injection_shaped_tag_is_refused_without_executing()
    {
        Shell.SkipOnWindows();
        var sentinel = Path.Combine(_root, "sentinel");

        var (result, _) = Verify($"v0.0.$(touch {sentinel})-alpha", _mergeCommit);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release tags must match v0.0.N-alpha", result.Stderr);
        Assert.False(File.Exists(sentinel), "the tag value was executed");
    }

    [Fact]
    public void Lightweight_tag_is_refused()
    {
        Shell.SkipOnWindows();
        Shell.Git(_clone, "tag", "v0.0.1-alpha", _mergeCommit);
        Shell.Git(_clone, "push", "origin", "refs/tags/v0.0.1-alpha");

        var (result, output) = Verify("v0.0.1-alpha", _mergeCommit);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release tags must be annotated", result.Stderr);
        Assert.Empty(output);
    }

    [Fact]
    public void Tag_on_commit_not_on_main_is_refused()
    {
        Shell.SkipOnWindows();
        PushAnnotatedTag("v0.0.1-alpha", _sideCommit);

        var (result, output) = Verify("v0.0.1-alpha", _sideCommit);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"tagged commit {_sideCommit} is not on main", result.Stderr);
        Assert.Empty(output);
    }

    [Fact]
    public void Tag_not_pointing_at_the_built_commit_is_refused()
    {
        Shell.SkipOnWindows();
        PushAnnotatedTag("v0.0.1-alpha", _mergeCommit);

        var (result, output) = Verify("v0.0.1-alpha", _plainMainCommit);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"does not match the commit being built ({_plainMainCommit})", result.Stderr);
        Assert.Empty(output);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Commit(string message)
    {
        File.WriteAllText(Path.Combine(_clone, "file.txt"), message);
        Shell.Git(_clone, "add", "file.txt");
        Shell.Git(_clone, "commit", "-m", message);
    }

    private void PushAnnotatedTag(string tag, string commit)
    {
        Shell.Git(_clone, "tag", "-a", tag, commit, "-m", $"Hoard {tag}");
        Shell.Git(_clone, "push", "origin", $"refs/tags/{tag}");
    }

    private (Shell.Result Result, string Output) Verify(string tag, string githubSha)
    {
        var outputFile = Path.Combine(_root, "github-output");
        File.WriteAllText(outputFile, "");

        var result = Shell.Run(
            _clone,
            "bash",
            [Path.Combine(Shell.RepositoryRoot, "eng", "release", "verify-tag.sh")],
            new Dictionary<string, string>
            {
                ["TAG"] = tag,
                ["GITHUB_SHA"] = githubSha,
                ["GITHUB_OUTPUT"] = outputFile,
            });

        return (result, File.ReadAllText(outputFile).Trim());
    }
}
