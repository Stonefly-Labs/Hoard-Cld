using System.Runtime.Versioning;
using Hoard.Host.Startup;
using Hoard.Host.Tests.Support;

namespace Hoard.Host.Tests.Startup;

public sealed class DataDirectoryTests : IDisposable
{
    private readonly TempDirectory work = new();

    public void Dispose() => work.Dispose();

    [Fact]
    public void A_relative_path_resolves_against_the_working_directory()
    {
        Assert.Equal(work.Combine("data"), DataDirectory.Resolve("./data", work.Path));
        Assert.Equal(work.Combine("a", "b"), DataDirectory.Resolve("a/b", work.Path));
        Assert.True(Path.IsPathFullyQualified(DataDirectory.Resolve("../x", work.Path)));
    }

    [Fact]
    public void A_missing_directory_and_its_parents_are_created()
    {
        var path = work.Combine("a", "b", "data");

        var directory = DataDirectory.Create(path);

        Assert.True(directory.Created);
        Assert.Equal(path, directory.Path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void An_existing_directory_is_reported_as_existing_and_its_mode_is_kept()
    {
        var path = work.Combine("data");
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }

        var directory = DataDirectory.Create(path);

        Assert.False(directory.Created);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal((UnixFileMode)0b111_101_101, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void A_new_directory_is_private_to_its_owner_on_unix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only");
        var path = work.Combine("data");

        DataDirectory.Create(path);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(path));
    }

    [Fact]
    public void The_write_check_leaves_nothing_behind()
    {
        var directory = DataDirectory.Create(work.Combine("data"));

        directory.WriteCheck();
        directory.WriteCheck();

        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }
}
