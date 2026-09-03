namespace Darya.Features.Notifications.RegisterDeviceToken;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Darya.Server.Data;
using Darya.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public record RegisterDeviceTokenCommand(
    string Token,
    string? Platform
);

public class RegisterDeviceTokenCommandHandler
{
    private readonly DaryaDbContext _db;
    private readonly ILogger<RegisterDeviceTokenCommandHandler> _logger;

    public RegisterDeviceTokenCommandHandler(DaryaDbContext db, ILogger<RegisterDeviceTokenCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(RegisterDeviceTokenCommand command, HttpContext context, CancellationToken ct = default)
    {
        var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Results.Json(new { message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Results.BadRequest(new { message = "Device token cannot be empty." });
        }

        var normalizedToken = command.Token.Trim();
        var platform = string.IsNullOrWhiteSpace(command.Platform) ? "android" : command.Platform.Trim().ToLowerInvariant();

        var existingToken = await _db.DevicePushTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == normalizedToken, ct);

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        if (existingToken != null)
        {
            existingToken.Platform = platform;
            existingToken.LastUpdatedAt = now;
            _logger.LogInformation("Updated existing device push token for user {UserId} (platform: {Platform})", userId, platform);
        }
        else
        {
            var deviceToken = new DevicePushToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = normalizedToken,
                Platform = platform,
                CreatedAt = now,
                LastUpdatedAt = now
            };

            _db.DevicePushTokens.Add(deviceToken);
            _logger.LogInformation("Registered new device push token for user {UserId} (platform: {Platform})", userId, platform);
        }

        await _db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Device push token registered successfully." });
    }
}
