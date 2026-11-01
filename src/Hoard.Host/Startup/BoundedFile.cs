namespace Hoard.Host.Startup;

/// <summary>
/// Reads the config file and certificate files: at most 1 MiB, and only files that report a size.
/// A FIFO or a device reports size 0 and is never opened, so it cannot hang startup (research R5).
/// </summary>
internal static class BoundedFile
{
    public const int Limit = 1024 * 1024;

    public enum Outcome
    {
        Read,
        Missing,
        Empty,
        NotRegular,
        TooLarge,
        Unreadable,
    }

    public static Outcome TryRead(string path, out byte[] content)
    {
        content = [];
        if (Directory.Exists(path))
        {
            return Outcome.NotRegular;
        }

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return Outcome.Missing;
        }

        if (info.Length == 0)
        {
            return Outcome.Empty;
        }

        if (info.Length > Limit)
        {
            return Outcome.TooLarge;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
            var buffer = new byte[Limit + 1];
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
            }

            if (total > Limit)
            {
                return Outcome.TooLarge;
            }

            content = buffer[..total];
            return Outcome.Read;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Outcome.Unreadable;
        }
    }

    /// <summary>Hoard's own wording for a failed read; never an exception message.</summary>
    public static string Describe(Outcome outcome) => outcome switch
    {
        Outcome.Missing => "the file does not exist",
        Outcome.Empty or Outcome.NotRegular => "the file is empty or not a regular file",
        Outcome.TooLarge => "the file is larger than 1 MiB",
        _ => "the file could not be read",
    };
}
