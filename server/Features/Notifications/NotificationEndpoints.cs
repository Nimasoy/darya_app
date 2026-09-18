namespace Darya.Features.Notifications;

using System.Security.Claims;
using Darya.Features.Notifications.RegisterDeviceToken;
using Darya.Features.Security;
using Darya.Server.Data;
using Darya.Server.Services.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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

        group.MapPost("/test-push", async (HttpContext context, INotificationService notificationService, DaryaDbContext db, CancellationToken ct) =>
        {
            var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

            var tokenCount = await db.DevicePushTokens.CountAsync(t => t.UserId == userId, ct);
            if (tokenCount == 0)
            {
                return Results.BadRequest(new 
                { 
                    message = "No registered device push tokens found for this account. Open the native Android app to automatically register your device token." 
                });
            }

            await notificationService.SendPushToUserAsync(
                userId, 
                "🌸 Darya Notification Test", 
                "Push notifications are working! Your device is successfully linked to Darya.", 
                new System.Collections.Generic.Dictionary<string, string> { { "type", "test" } }, 
                ct);

            return Results.Ok(new 
            { 
                message = $"Test push notification sent successfully to {tokenCount} device(s)." 
            });
        })
        .WithName("TestPushNotification")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

        return app;
    }
}
