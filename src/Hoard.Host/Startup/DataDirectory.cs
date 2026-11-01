namespace Hoard.Host.Startup;

/// <summary>
/// The single data directory (research R6). Nothing in this spec stores data; the write check is
/// the only file Hoard creates there, and it deletes itself.
/// </summary>
internal sealed class DataDirectory
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private DataDirectory(string path, bool created)
    {
        Path = path;
        Created = created;
    }

    public string Path { get; }

    /// <summary>Whether this run created the directory.</summary>
    public bool Created { get; }

    public static string Resolve(string value, string workingDirectory) => System.IO.Path.GetFullPath(value, workingDirectory);

    /// <summary>
    /// Creates the directory and missing parents (mode 0700 on Unix) or uses the existing one,
    /// whose mode is left alone. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    public static DataDirectory Create(string path)
    {
        if (Directory.Exists(path))
        {
            return new DataDirectory(path, created: false);
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, OwnerOnly);
        }

        return new DataDirectory(path, created: true);
    }

    /// <summary>Creates, writes and removes one file; throws if the directory is not usable.</summary>
    public void WriteCheck() => WriteCheck(".hoard-write-check-" + System.IO.Path.GetRandomFileName());

    /// <summary>
    /// <see cref="FileMode.CreateNew"/> refuses an existing file or symlink rather than following it.
    /// </summary>
    internal void WriteCheck(string fileName)
    {
        using var stream = new FileStream(
            System.IO.Path.Combine(Path, fileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose);
        stream.WriteByte(0);
        stream.Flush(flushToDisk: false);
    }
}
