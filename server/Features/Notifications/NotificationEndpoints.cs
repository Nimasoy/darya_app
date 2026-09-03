namespace Darya.Features.Notifications;

using Darya.Features.Notifications.RegisterDeviceToken;
using Darya.Features.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapPost("/device-token", async (RegisterDeviceTokenCommand command, HttpContext context, RegisterDeviceTokenCommandHandler handler, CancellationToken ct) =>
        {
            return await handler.HandleAsync(command, context, ct);
        })
        .WithName("RegisterDeviceToken")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

        return app;
    }
}
