using System.Diagnostics;
using System.Reflection;

namespace Hoard.Release.Tests;

/// <summary>Runs the repository's release scripts and the git commands that set up their inputs.</summary>
internal static class Shell
{
    public static string RepositoryRoot { get; } =
        typeof(Shell).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "RepositoryRoot")
            .Value!;

    public static void SkipOnWindows() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The release scripts run on Linux and macOS runners only.");

    public static Result Run(string workingDirectory, string file, string[] args, IDictionary<string, string>? env = null)
    {
        var start = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Isolate git from the developer's config (signing, hooks, default branch).
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Hoard Tests";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "tests@hoard.invalid";
        foreach (var (key, value) in env ?? new Dictionary<string, string>())
        {
            start.Environment[key] = value;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new Result(process.ExitCode, stdout.Result, stderr.Result);
    }

    public static string Git(string workingDirectory, params string[] args)
    {
        var result = Run(workingDirectory, "git", args);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', args)} failed: {result.Stderr}");
        return result.Stdout.Trim();
    }

    public sealed record Result(int ExitCode, string Stdout, string Stderr);
}
