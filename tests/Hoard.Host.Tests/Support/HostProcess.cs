using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Hoard.Host.Tests.Support;

/// <summary>
/// Runs <c>dotnet Hoard.Host.dll</c> with an empty environment plus the given variables, in a
/// given working directory, capturing everything it writes (research R12).
/// </summary>
internal sealed class HostProcess : IAsyncDisposable
{
    private static readonly string HostPath = typeof(HostProcess).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "HoardHostPath").Value!;

    // <dotnet root>/shared/Microsoft.NETCore.App/<version>/
    private static readonly string Muxer = Path.Combine(
        Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..")),
        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");

    private readonly System.Diagnostics.Process process;
    private readonly StringBuilder stdout = new();
    private readonly StringBuilder stderr = new();
    private readonly Lock gate = new();

    private HostProcess(System.Diagnostics.Process process)
    {
        this.process = process;
    }

    public string StandardOutput
    {
        get
        {
            lock (gate)
            {
                return stdout.ToString();
            }
        }
    }

    public string StandardError
    {
        get
        {
            lock (gate)
            {
                return stderr.ToString();
            }
        }
    }

    public string Output => StandardOutput + StandardError;

    public bool HasExited => process.HasExited;

    public static HostProcess Start(string workingDirectory, IEnumerable<string>? args = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(Muxer)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(HostPath);
        foreach (var arg in args ?? [])
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment.Clear();
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        var process = new System.Diagnostics.Process { StartInfo = info, EnableRaisingEvents = true };
        var host = new HostProcess(process);
        process.OutputDataReceived += (_, e) => host.Append(host.stdout, e.Data);
        process.ErrorDataReceived += (_, e) => host.Append(host.stderr, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return host;
    }

    /// <summary>Waits until <paramref name="text"/> appears in the output; false if the process exits or time runs out first.</summary>
    public async Task<bool> WaitForOutputAsync(string text, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < deadline)
        {
            if (Output.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (process.HasExited)
            {
                // Let the output readers drain.
                await process.WaitForExitAsync();
                return Output.Contains(text, StringComparison.Ordinal);
            }

            await Task.Delay(50);
        }

        return false;
    }

    /// <summary>Waits for exit and returns the exit code; throws if the process is still running after <paramref name="timeout"/>.</summary>
    public async Task<int> WaitForExitAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(cts.Token);
        return process.ExitCode;
    }

    public void SendSigterm()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("SIGTERM is not available on Windows.");
        }

        _ = Kill(process.Id, 15);
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    private void Append(StringBuilder target, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            target.AppendLine(line);
        }
    }
}
