using System.Runtime.Versioning;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

/// <summary>Unusable data directories (US2 sc.4, research R6).</summary>
public sealed class DataDirectoryErrorTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void An_unwritable_directory_fails_naming_the_path()
    {
        SkipUnlessPermissionsApply();
        var path = work.Combine("data");
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var problem = Assert.Single(Options.Validate(work.Path).Problems);

        Assert.Equal("default data.path", problem.Location);
        Assert.Contains(path, problem.Message, StringComparison.Ordinal);
        Assert.Contains("not writable", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void A_directory_that_cannot_be_created_fails_naming_the_path()
    {
        SkipUnlessPermissionsApply();
        var parent = work.Combine("locked");
        Directory.CreateDirectory(parent);
        File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var path = Path.Combine(parent, "data");

        var problem = Assert.Single(Options.Validate(work.Path, ["--data:path", path]).Problems);

        Assert.Equal("command line --data:path", problem.Location);
        Assert.Contains(path, problem.Message, StringComparison.Ordinal);
        Assert.Contains("could not be created", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_parent_that_is_a_file_fails_naming_the_path()
    {
        work.Write("file", "x");
        var path = work.Combine("file", "data");

        var problem = Assert.Single(Options.Validate(work.Path, ["--data:path", path]).Problems);

        Assert.Contains("could not be created", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_write_check_refuses_an_existing_symlink_instead_of_following_it()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "symlinks need privileges on Windows");
        var directory = DataDirectory.Create(work.Combine("data"));
        var target = work.Combine("target");
        File.Create(work.Combine("data", ".hoard-write-check-planted")).Dispose();
        File.Delete(work.Combine("data", ".hoard-write-check-planted"));
        File.CreateSymbolicLink(work.Combine("data", ".hoard-write-check-planted"), target);

        Assert.ThrowsAny<IOException>(() => directory.WriteCheck(".hoard-write-check-planted"));

        Assert.False(File.Exists(target));
    }

    private static void SkipUnlessPermissionsApply()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only");
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root ignores file modes");
    }
}
