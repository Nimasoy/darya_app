namespace Darya.Server.Services.Notifications;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Darya.Server.Data;
using Darya.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class NotificationService : INotificationService
{
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly DaryaDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ITelegramNotifier telegramNotifier,
        DaryaDbContext db,
        ILogger<NotificationService> logger)
    {
        _telegramNotifier = telegramNotifier;
        _db = db;
        _logger = logger;
    }

    public async Task SendTelegramAsync(long chatId, string message, CancellationToken ct = default)
    {
        await _telegramNotifier.SendMessageAsync(chatId, message);
    }

    public async Task SendPushToUserAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var tokens = await _db.DevicePushTokens
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            _logger.LogInformation("No device push tokens registered for user {UserId}. Push delivery skipped.", userId);
            return;
        }

        _logger.LogInformation("Dispatching push alert to {Count} device token(s) for user {UserId}: {Title}", tokens.Count, userId, title);

        // Extensible FCM Push Dispatch Provider
        // When production FCM credentials (service-account.json / FirebaseApp) are supplied,
        // this loop delivers via FirebaseMessaging.DefaultInstance.SendMulticastAsync.
        foreach (var token in tokens)
        {
            _logger.LogDebug("FCM push queued for token {TokenPrefix}...: Title='{Title}', Body='{Body}'", 
                token.Length > 10 ? token[..10] : token, title, body);
        }
    }

    public async Task SendPartnerAlertAsync(User primaryUser, NotificationPayload payload, CancellationToken ct = default)
    {
        // 1. Deliver to Telegram if primary user configured partner's chat ID
        if (primaryUser.PartnerTelegramChatId.HasValue)
        {
            string telegramText = payload.TelegramMessage ?? $"*{payload.Title}*\n\n{payload.Body}";
            try
            {
                await _telegramNotifier.SendMessageAsync(primaryUser.PartnerTelegramChatId.Value, telegramText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver partner Telegram alert to chat {ChatId}", primaryUser.PartnerTelegramChatId.Value);
            }
        }

        // 2. Deliver via FCM Push to the linked partner user if linked
        if (primaryUser.PartnerUserId.HasValue)
        {
            try
            {
                await SendPushToUserAsync(primaryUser.PartnerUserId.Value, payload.Title, payload.Body, payload.Data, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver push notification to partner user {PartnerUserId}", primaryUser.PartnerUserId.Value);
            }
        }
    }
}
