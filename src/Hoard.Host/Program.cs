using System.Collections;
using System.Runtime.InteropServices;
using Hoard.Host.Startup;

// Signals are handled from the first line, before the host's own console lifetime exists, so a
// stop signal during startup also exits cleanly with code 0 (research R8).
using var stop = new CancellationTokenSource();
using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);

var environment = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>()
    .ToDictionary(e => (string)e.Key, e => (string?)e.Value ?? "", StringComparer.Ordinal);

return await HoardStartup.RunAsync(args, environment, Directory.GetCurrentDirectory(), Console.Error, stop.Token);

void Stop(PosixSignalContext context)
{
    context.Cancel = true;
    stop.Cancel();
}
