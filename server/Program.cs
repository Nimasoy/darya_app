using Microsoft.EntityFrameworkCore;
using Darya.Server.Data;
using Darya.Server.Models;
using Darya.Server.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Ensure JSON serialization preserves Enums as strings (or integers, let's use strings for frontend readability)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Configure CORS to allow Vite development client & potential production clients
// Configure CORS to allow Vite development client & production domains
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173", 
            "http://localhost:3000", 
            "http://localhost:80", 
            "http://localhost",
            "https://darya-app.space",
            "https://api.darya-app.space"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Configure SQLite Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=darya.db";
builder.Services.AddDbContext<DaryaDbContext>(options =>
    options.UseSqlite(connectionString));

// Register Services
builder.Services.AddScoped<ICycleEngine, CycleEngine>();

// Configure HTTP Client for Cloudflare worker proxy support (essential for bypassing network filters in Iran)
builder.Services.AddHttpClient<ITelegramNotifier, TelegramNotifier>();

var app = builder.Build();

// Ensure Database is created and seeded automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DaryaDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowClient");

// Base endpoint
app.MapGet("/", () => "Darya Personalized Menstrual Cycle Management API is running.");

// 1. GET Current Cycle Phase, Status and Advice
app.MapGet("/api/cycle/current", async (
    HttpContext context,
    DaryaDbContext db,
    ICycleEngine cycleEngine) =>
{
    // Try to get userId from header, else default to the seeded user Guid
    var userIdHeader = context.Request.Headers["X-User-Id"].ToString();
    var userId = Guid.TryParse(userIdHeader, out var parsedGuid) 
        ? parsedGuid 
        : Guid.Parse("d8a7a8d8-a8d8-4a8d-b8d8-d8a7a8d8a7a8");

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
    {
        // Try getting first user
        user = await db.Users.FirstOrDefaultAsync();
        if (user == null)
        {
            return Results.NotFound(new { message = "No users found in database." });
        }
    }

    var status = cycleEngine.CalculateStatus(user, DateTime.UtcNow);

    return Results.Ok(new
    {
        userId = user.Id,
        name = user.Name,
        averageCycleLength = user.AverageCycleLength,
        averagePeriodLength = user.AveragePeriodLength,
        lastPeriodStartDate = user.LastPeriodStartDate,
        partnerTelegramChatId = user.PartnerTelegramChatId,
        language = user.Language,
        currentDay = status.CurrentDay,
        phase = status.Phase,
        phaseName = status.PhaseName,
        daysUntilNextPeriod = status.DaysUntilNextPeriod,
        partnerAdvice = status.PartnerAdvice
    });
});

// 2. POST Save Daily Log & Send partner notification
app.MapPost("/api/logs", async (
    DailyLogRequest request,
    HttpContext context,
    DaryaDbContext db,
    ICycleEngine cycleEngine,
    ITelegramNotifier notifier) =>
{
    var userIdHeader = context.Request.Headers["X-User-Id"].ToString();
    var userId = Guid.TryParse(userIdHeader, out var parsedGuid) 
        ? parsedGuid 
        : (request.UserId != Guid.Empty ? request.UserId : Guid.Parse("d8a7a8d8-a8d8-4a8d-b8d8-d8a7a8d8a7a8"));

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
    {
        return Results.NotFound(new { message = "User not found." });
    }

    var date = request.Date?.Date ?? DateTime.UtcNow.Date;

    // Check if daily log already exists for this date, if so, update it
    var existingLog = await db.DailyLogs.FirstOrDefaultAsync(l => l.UserId == userId && l.Date.Date == date);
    if (existingLog != null)
    {
        existingLog.FlowIntensity = request.FlowIntensity;
        existingLog.Mood = request.Mood;
        existingLog.PainLevel = Math.Clamp(request.PainLevel, 0, 5);
        existingLog.Note = request.Note ?? string.Empty;
    }
    else
    {
        var newLog = new DailyLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = date,
            FlowIntensity = request.FlowIntensity,
            Mood = request.Mood,
            PainLevel = Math.Clamp(request.PainLevel, 0, 5),
            Note = request.Note ?? string.Empty
        };
        db.DailyLogs.Add(newLog);
    }

    await db.SaveChangesAsync();

    // Trigger Telegram notification to the partner if configured
    if (user.PartnerTelegramChatId.HasValue)
    {
        var status = cycleEngine.CalculateStatus(user, DateTime.UtcNow);
        string message;

        if (user.Language == "fa")
        {
            string moodEmoji = request.Mood switch
            {
                Mood.Calm => "🧘 آروم",
                Mood.Happy => "☀️ خوشحال",
                Mood.Sensitive => "💖 حساس",
                Mood.LowEnergy => "🥱 بی‌حوصله",
                Mood.Anxious => "💭 مضطرب",
                Mood.Irritable => "⚡ کلافه",
                _ => request.Mood.ToString()
            };

            string flowName = request.FlowIntensity switch
            {
                FlowIntensity.None => "هیچی",
                FlowIntensity.Light => "کم",
                FlowIntensity.Medium => "متوسط",
                FlowIntensity.Heavy => "زیاد",
                _ => request.FlowIntensity.ToString()
            };

            string painStars = new string('★', request.PainLevel) + new string('☆', 5 - request.PainLevel);
            string noteText = string.IsNullOrWhiteSpace(request.Note) ? "_بدون یادداشت_" : $"\"{request.Note}\"";
            string phaseFarsi = status.PhaseName switch
            {
                "Menstrual" => "پریود (قاعدگی)",
                "Follicular" => "فولیکولار",
                "Ovulatory" => "تخمک‌گذاری",
                "Luteal" => "لوتئال",
                _ => status.PhaseName
            };

            message = 
                $"🌸 *گزارش روزانه {user.Name}* 🌸\n\n" +
                $"*وضعیت دوره:* روز {status.CurrentDay} از {user.AverageCycleLength} (فاز {phaseFarsi})\n" +
                $"*روزهای مانده تا دوره بعدی:* {status.DaysUntilNextPeriod} روز\n\n" +
                $"*ثبت وضعیت امروز:*\n" +
                $"• حال و هوا: {moodEmoji}\n" +
                $"• میزان درد: {painStars} ({request.PainLevel}/5)\n" +
                $"• شدت خون‌ریزی: {flowName}\n" +
                $"• یادداشت: {noteText}\n\n" +
                $"💡 *پیشنهاد برای همسفرش:*\n" +
                $"{status.PartnerAdvice}";
        }
        else
        {
            string moodEmoji = request.Mood switch
            {
                Mood.Calm => "🧘 Calm",
                Mood.Happy => "☀️ Happy",
                Mood.Sensitive => "💖 Sensitive",
                Mood.LowEnergy => "🥱 Low Energy",
                Mood.Anxious => "💭 Anxious",
                Mood.Irritable => "⚡ Irritable",
                _ => request.Mood.ToString()
            };

            string painStars = new string('★', request.PainLevel) + new string('☆', 5 - request.PainLevel);
            string noteText = string.IsNullOrWhiteSpace(request.Note) ? "_No extra note written_" : $"\"{request.Note}\"";

            message = 
                $"🌸 *Darya Daily Update for {user.Name}* 🌸\n\n" +
                $"*Cycle Status:* Day {status.CurrentDay} of {user.AverageCycleLength} ({status.PhaseName} Phase)\n" +
                $"*Days until next cycle:* {status.DaysUntilNextPeriod} days\n\n" +
                $"*Today's Check-in:*\n" +
                $"• Mood: {moodEmoji}\n" +
                $"• Pain Level: {painStars} ({request.PainLevel}/5)\n" +
                $"• Flow Intensity: {request.FlowIntensity}\n" +
                $"• Note: {noteText}\n\n" +
                $"💡 *How to support her today:*\n" +
                $"{status.PartnerAdvice}";
        }

        await notifier.SendMessageAsync(user.PartnerTelegramChatId.Value, message);
    }

    return Results.Ok(new { message = "Daily log saved successfully." });
});

// 3. POST Configure User Setup & dates
app.MapPost("/api/user/setup", async (
    UserSetupRequest request,
    HttpContext context,
    DaryaDbContext db,
    ICycleEngine cycleEngine,
    ITelegramNotifier notifier) =>
{
    var userIdHeader = context.Request.Headers["X-User-Id"].ToString();
    var userId = Guid.TryParse(userIdHeader, out var parsedGuid) 
        ? parsedGuid 
        : Guid.Parse("d8a7a8d8-a8d8-4a8d-b8d8-d8a7a8d8a7a8");

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    bool isNewUser = false;

    if (user == null)
    {
        user = new User { Id = userId };
        db.Users.Add(user);
        isNewUser = true;
    }

    user.Name = string.IsNullOrWhiteSpace(request.Name) ? "Darya" : request.Name;
    user.AverageCycleLength = request.AverageCycleLength > 0 ? request.AverageCycleLength : 28;
    user.AveragePeriodLength = request.AveragePeriodLength > 0 ? request.AveragePeriodLength : 5;
    user.LastPeriodStartDate = request.LastPeriodStartDate?.Date ?? DateTime.UtcNow.Date.AddDays(-10);
    user.PartnerTelegramChatId = request.PartnerTelegramChatId;
    user.Language = string.IsNullOrWhiteSpace(request.Language) ? "fa" : request.Language;

    await db.SaveChangesAsync();

    // Trigger confirmation notification on Telegram if TelegramChatId is set
    if (user.PartnerTelegramChatId.HasValue)
    {
        var status = cycleEngine.CalculateStatus(user, DateTime.UtcNow);
        string message;

        if (user.Language == "fa")
        {
            string phaseFarsi = status.PhaseName switch
            {
                "Menstrual" => "پریود (قاعدگی)",
                "Follicular" => "فولیکولار",
                "Ovulatory" => "تخمک‌گذاری",
                "Luteal" => "لوتئال",
                _ => status.PhaseName
            };

            message = 
                $"🔔 *اپلیکیشن دریا تنظیم شد!* 🔔\n\n" +
                $"سلام! از این به بعد شما پیام‌های منظمی دریافت می‌کنید تا بتونید از *{user.Name}* توی دوره‌اش بهتر حمایت کنید.\n\n" +
                $"*وضعیت فعلی:* روز {status.CurrentDay} از {user.AverageCycleLength} (فاز {phaseFarsi})\n\n" +
                $"💡 *نکته امروز:*\n" +
                $"{status.PartnerAdvice}";
        }
        else
        {
            message = 
                $"🔔 *Darya App Configured!* 🔔\n\n" +
                $"Hello! You will now receive regular updates to help you support *{user.Name}* during her cycle.\n\n" +
                $"*Current State:* Day {status.CurrentDay} of {user.AverageCycleLength} ({status.PhaseName} Phase)\n\n" +
                $"💡 *Tip for today:*\n" +
                $"{status.PartnerAdvice}";
        }

        await notifier.SendMessageAsync(user.PartnerTelegramChatId.Value, message);
    }

    return Results.Ok(new 
    { 
        message = isNewUser ? "User setup created successfully." : "User profile updated successfully.",
        userId = user.Id
    });
});

app.Run();

// DTO Requests models
public record DailyLogRequest(
    Guid UserId,
    DateTime? Date,
    FlowIntensity FlowIntensity,
    Mood Mood,
    int PainLevel,
    string? Note
);

public record UserSetupRequest(
    string Name,
    int AverageCycleLength,
    int AveragePeriodLength,
    DateTime? LastPeriodStartDate,
    long? PartnerTelegramChatId,
    string? Language
);
