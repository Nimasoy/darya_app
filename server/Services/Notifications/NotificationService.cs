namespace Darya.Server.Services.Notifications;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Darya.Server.Data;
using Darya.Server.Models;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class NotificationService : INotificationService
{
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly DaryaDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    private static readonly object _initLock = new();
    private static bool _isFirebaseInitialized = false;

    public NotificationService(
        ITelegramNotifier telegramNotifier,
        DaryaDbContext db,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _telegramNotifier = telegramNotifier;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    private void EnsureFirebaseInitialized()
    {
        if (_isFirebaseInitialized) return;

        lock (_initLock)
        {
            if (_isFirebaseInitialized) return;

            try
            {
                var configPath = _configuration["Firebase:ServiceAccountPath"]
                    ?? Environment.GetEnvironmentVariable("FIREBASE_CONFIG_PATH")
                    ?? Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");

                // Also check project root / working dir if not in AppContext.BaseDirectory
                if (!File.Exists(configPath) && File.Exists("firebase-service-account.json"))
                {
                    configPath = "firebase-service-account.json";
                }

                if (File.Exists(configPath))
                {
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(configPath)
                        });
                        _logger.LogInformation("Firebase Admin SDK initialized successfully using {Path}", configPath);
                    }
                    _isFirebaseInitialized = true;
                }
                else
                {
                    _logger.LogWarning("Firebase service account credentials not found at '{Path}'. Push notifications will be simulated in logs.", configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase Admin SDK.");
            }
        }
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

        EnsureFirebaseInitialized();

        if (FirebaseApp.DefaultInstance != null)
        {
            try
            {
                var multicastMessage = new MulticastMessage
                {
                    Tokens = tokens,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = data
                };

                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage, ct);
                _logger.LogInformation("FCM dispatch complete for user {UserId}: {SuccessCount} succeeded, {FailureCount} failed out of {TotalCount}",
                    userId, response.SuccessCount, response.FailureCount, tokens.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching FCM multicast message to user {UserId}", userId);
            }
        }
        else
        {
            _logger.LogInformation("FCM Simulation (Awaiting firebase-service-account.json): Dispatched push notification to {Count} token(s). Title: '{Title}', Body: '{Body}'",
                tokens.Count, title, body);
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
