using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Darya.Server.Services
{
    public interface ITelegramNotifier
    {
        Task SendMessageAsync(long chatId, string message);
    }

    public class TelegramNotifier : ITelegramNotifier
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TelegramNotifier> _logger;
        private readonly string? _botToken;

        public TelegramNotifier(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramNotifier> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _botToken = configuration["Telegram:BotToken"];
        }

        public async Task SendMessageAsync(long chatId, string message)
        {
            if (string.IsNullOrWhiteSpace(_botToken))
            {
                _logger.LogWarning("Telegram Bot Token is not configured in settings. Message to chat {ChatId} logged to console: {Message}", chatId, message);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[MOCK TELEGRAM NOTIFICATION TO CHAT {chatId}]");
                Console.WriteLine(message);
                Console.ResetColor();
                return;
            }

            string url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            };

            try
            {
                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent Telegram alert to partner Chat ID {ChatId}", chatId);
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send Telegram message to Chat ID {ChatId}. Status: {StatusCode}, Error: {Error}", 
                        chatId, response.StatusCode, errorResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending Telegram notification to Chat ID {ChatId}", chatId);
            }
        }
    }
}
