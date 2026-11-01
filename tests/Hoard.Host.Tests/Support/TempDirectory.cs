using System.Runtime.InteropServices;

namespace Hoard.Host.Tests.Support;

/// <summary>A fresh directory under the system temp directory, removed on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        // The real path: on macOS the temp directory sits behind the /var -> /private/var link, and
        // a process started there reports the resolved working directory.
        var created = Directory.CreateTempSubdirectory("hoard-test-").FullName;
        Path = OperatingSystem.IsWindows() ? created : RealPath(created);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string Write(string name, string content)
    {
        var path = Combine(name);
        File.WriteAllText(path, content);
        return path;
    }

    [DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi)]
    private static extern IntPtr RealPath(string path, IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

    private static string RealPath(string path)
    {
        var resolved = RealPath(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
        {
            return path;
        }

        try
        {
            return Marshal.PtrToStringAnsi(resolved) ?? path;
        }
        finally
        {
            Free(resolved);
        }
    }

    public void Dispose()
    {
        // Tests make directories unwritable; give everything back before removing it.
        if (!OperatingSystem.IsWindows())
        {
            foreach (var directory in Directory.EnumerateDirectories(Path, "*", SearchOption.AllDirectories).Prepend(Path))
            {
                try
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
