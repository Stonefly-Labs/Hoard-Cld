using Hoard.Host.Startup;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Hoard.Host.Features.Health;

/// <summary>
/// <c>/health/live</c> (no checks) and <c>/health/ready</c> (checks tagged <c>ready</c>), GET and
/// HEAD only (contracts/health-endpoints.md). Later specs add readiness by registering an
/// <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/> with the <c>ready</c> tag.
/// </summary>
internal static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    private static readonly string[] Methods = [HttpMethods.Get, HttpMethods.Head];

    public static void AddHealth(this IServiceCollection services, DataDirectory dataDirectory)
    {
        services.AddHealthChecks()
            .AddCheck<StartupCheck>("startup", tags: [ReadyTag])
            .AddCheck("data-directory", new DataDirectoryCheck(dataDirectory.WriteCheck), tags: [ReadyTag]);
    }

    public static void MapHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .WithMetadata(new HttpMethodMetadata(Methods));
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .WithMetadata(new HttpMethodMetadata(Methods));
    }
}
