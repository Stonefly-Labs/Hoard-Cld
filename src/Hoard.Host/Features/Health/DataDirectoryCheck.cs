using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hoard.Host.Features.Health;

/// <summary>
/// Runs the data-directory write check on every probe, with no caching (research R6). Concurrent
/// probes share the check in flight, and one that takes longer than <see cref="Timeout"/> reports
/// unhealthy, so a hung filesystem or a probe flood cannot pile up file operations.
/// </summary>
internal sealed class DataDirectoryCheck(Action writeCheck, TimeSpan timeout) : IHealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly Lock gate = new();
    private Task? inFlight;

    public DataDirectoryCheck(Action writeCheck)
        : this(writeCheck, Timeout)
    {
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Task check;
        lock (gate)
        {
            if (inFlight is null || inFlight.IsCompleted)
            {
                inFlight = Task.Run(writeCheck, CancellationToken.None);
            }

            check = inFlight;
        }

        try
        {
            await check.WaitAsync(timeout, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
