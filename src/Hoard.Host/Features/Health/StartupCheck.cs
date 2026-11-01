using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hoard.Host.Features.Health;

/// <summary>Ready only between <c>ApplicationStarted</c> and <c>ApplicationStopping</c> (research R7).</summary>
internal sealed class StartupCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(lifetime.ApplicationStarted.IsCancellationRequested && !lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy());
}
