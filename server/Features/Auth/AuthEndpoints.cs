namespace Darya.Features.Auth;

using System.Security.Claims;
using Darya.Features.Auth.Login;
using Darya.Features.Auth.Register;
using Darya.Features.Security;
using Darya.Server.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterCommand command, RegisterCommandHandler handler, CancellationToken ct) =>
        {
            return await handler.HandleAsync(command, ct);
        })
        .WithName("Register")
        .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/login", async (LoginCommand command, LoginCommandHandler handler, CancellationToken ct) =>
        {
            return await handler.HandleAsync(command, ct);
        })
        .WithName("Login")
        .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapGet("/me", async (HttpContext context, DaryaDbContext db, CancellationToken ct) =>
        {
            var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Results.Json(new { message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            string? partnerName = null;
            if (user.PartnerUserId.HasValue)
            {
                var partner = await db.Users.FirstOrDefaultAsync(u => u.Id == user.PartnerUserId.Value, ct);
                partnerName = partner?.Name;
            }

            return Results.Ok(new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                role = user.Role,
                averageCycleLength = user.AverageCycleLength,
                averagePeriodLength = user.AveragePeriodLength,
                lastPeriodStartDate = user.LastPeriodStartDate,
                partnerTelegramChatId = user.PartnerTelegramChatId,
                language = user.Language,
                partnerCode = user.PartnerCode,
                partnerUserId = user.PartnerUserId,
                partnerName,
                isLinked = user.PartnerUserId.HasValue
            });
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

        return app;
    }
}
