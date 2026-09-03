namespace Darya.Server.Services.Notifications;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Darya.Server.Models;

public record NotificationPayload(
    string Title,
    string Body,
    string? TelegramMessage = null,
    Dictionary<string, string>? Data = null
);

public interface INotificationService
{
    Task SendPartnerAlertAsync(User primaryUser, NotificationPayload payload, CancellationToken ct = default);
    Task SendTelegramAsync(long chatId, string message, CancellationToken ct = default);
    Task SendPushToUserAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default);
}
