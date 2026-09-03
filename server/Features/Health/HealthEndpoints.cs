namespace Darya.Features.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness probe (lightweight process health check)
        app.MapGet("/healthz", async (GetHealthQueryHandler handler, CancellationToken ct) =>
        {
            var report = await handler.HandleAsync(new GetHealthQuery(CheckDependencies: false), ct);
            return Results.Ok(report);
        })
        .WithName("GetLivenessHealth")
        .WithTags("Health")
        .AllowAnonymous();

        // Readiness probe (verifies database dependency status)
        app.MapGet("/readyz", async (GetHealthQueryHandler handler, CancellationToken ct) =>
        {
            var report = await handler.HandleAsync(new GetHealthQuery(CheckDependencies: true), ct);
            return report.Status == "Healthy" 
                ? Results.Ok(report) 
                : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("GetReadinessHealth")
        .WithTags("Health")
        .AllowAnonymous();

        return app;
    }
}
