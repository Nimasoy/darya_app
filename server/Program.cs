using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Darya.Server.Data;
using Darya.Server.Models;
using Darya.Server.Services;
using System.Text.Json.Serialization;
using Darya.Features.Health;
using Darya.Features.Security;
using Darya.Features.Auth;
using Darya.Features.Auth.Login;
using Darya.Features.Auth.Register;
using Darya.Server.Services.Notifications;
using Darya.Features.Notifications;
using Darya.Features.Notifications.RegisterDeviceToken;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to handle Enums as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Configure CORS to dynamically allow Capacitor and local origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// Configure PostgreSQL Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=darya;Username=darya_user;Password=darya_password";
builder.Services.AddDbContext<DaryaDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Services
builder.Services.AddScoped<ICycleEngine, CycleEngine>();
builder.Services.AddScoped<GetHealthQueryHandler>();
builder.Services.AddDaryaRateLimiting();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<RegisterCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<RegisterDeviceTokenCommandHandler>();

// Configure HTTP Client for Telegram with proxy support (if proxy config is set)
var proxyUrl = builder.Configuration["Telegram:Proxy"];
if (!string.IsNullOrWhiteSpace(proxyUrl))
{
    builder.Services.AddHttpClient<ITelegramNotifier, TelegramNotifier>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy(proxyUrl),
            UseProxy = true
        });
}
else
{
    builder.Services.AddHttpClient<ITelegramNotifier, TelegramNotifier>();
}

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_darya_key_must_be_long_and_secure_256_bits";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DaryaIssuer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DaryaAudience";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Automatically run EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DaryaDbContext>();
    db.Database.Migrate();
}

app.UseRouting();
app.UseCors("AllowClient");
// app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Base health endpoint
app.MapGet("/", () => "Darya Production-grade Multi-Tenant API is running.");

// ==========================================
// 1. AUTHENTICATION ENDPOINTS (CQRS Feature Slice)
// ==========================================
app.MapAuthEndpoints();

// ==========================================
// 2. PARTNER LINKING ENDPOINTS
// ==========================================

// POST /api/partner/generate-code
app.MapPost("/api/partner/generate-code", async (HttpContext context, DaryaDbContext db) =>
{
    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null) return Results.NotFound();

    if (user.Role != Role.PrimaryUser)
    {
        return Results.BadRequest(new { message = "Only primary cycle trackers can generate linking codes." });
    }

    // Generate random 6-character code
    var randomCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
    
    // Ensure uniqueness
    while (await db.Users.AnyAsync(u => u.PartnerCode == randomCode))
    {
        randomCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
    }

    user.PartnerCode = randomCode;
    await db.SaveChangesAsync();

    return Results.Ok(new { code = randomCode });
}).RequireAuthorization();

// POST /api/partner/link
app.MapPost("/api/partner/link", async (LinkPartnerRequest request, HttpContext context, DaryaDbContext db, ICycleEngine cycleEngine, INotificationService notificationService) =>
{
    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

    var partnerUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (partnerUser == null) return Results.NotFound();

    if (partnerUser.Role != Role.Partner)
    {
        return Results.BadRequest(new { message = "Only partner accounts can submit linking codes." });
    }

    if (string.IsNullOrWhiteSpace(request.Code))
    {
        return Results.BadRequest(new { message = "Code is required." });
    }

    var code = request.Code.Trim().ToUpper();
    var primaryUser = await db.Users.FirstOrDefaultAsync(u => u.PartnerCode == code);
    if (primaryUser == null)
    {
        return Results.BadRequest(new { message = "Invalid or expired link code." });
    }

    if (primaryUser.Id == partnerUser.Id)
    {
        return Results.BadRequest(new { message = "You cannot link to your own account." });
    }

    // Process link
    var now = DateTime.UtcNow;
    primaryUser.PartnerUserId = partnerUser.Id;
    primaryUser.LinkedAt = now;
    primaryUser.PartnerCode = null; // Clear code so it cannot be reused

    partnerUser.PartnerUserId = primaryUser.Id;
    partnerUser.LinkedAt = now;

    await db.SaveChangesAsync();

    // Trigger alert across notification channels (Telegram / FCM Push)
    if (primaryUser.PartnerTelegramChatId.HasValue || primaryUser.PartnerUserId.HasValue)
    {
        var status = cycleEngine.CalculateStatus(primaryUser, DateTime.UtcNow);
        string title = primaryUser.Language == "fa" ? "اتصال همسفر با موفقیت انجام شد!" : "Partner Account Linked!";
        string body = primaryUser.Language == "fa"
            ? $"کاربر {partnerUser.Name} به عنوان همسفر به حساب شما متصل شد و می‌تواند وضعیت دوره شما را رصد کند. وضعیت امروز: روز {status.CurrentDay} از {primaryUser.AverageCycleLength} (فاز {TranslatePhaseToFarsi(status.PhaseName)})"
            : $"{partnerUser.Name} has linked successfully as your partner and can now track your cycle updates. Current State: Day {status.CurrentDay} of {primaryUser.AverageCycleLength} ({status.PhaseName} Phase)";
        string telegramMessage = primaryUser.Language == "fa"
            ? $"🔔 *اتصال همسفر با موفقیت انجام شد!* 🔔\n\n" +
              $"کاربر *{partnerUser.Name}* به عنوان همسفر به حساب شما متصل شد و می‌تواند وضعیت دوره شما را رصد کند.\n\n" +
              $"*وضعیت امروز:* روز {status.CurrentDay} از {primaryUser.AverageCycleLength} (فاز {TranslatePhaseToFarsi(status.PhaseName)})"
            : $"🔔 *Partner Account Linked!* 🔔\n\n" +
              $"*{partnerUser.Name}* has linked successfully as your partner and can now track your cycle updates.\n\n" +
              $"*Current State:* Day {status.CurrentDay} of {primaryUser.AverageCycleLength} ({status.PhaseName} Phase)";

        await notificationService.SendPartnerAlertAsync(primaryUser, new NotificationPayload(title, body, telegramMessage));
    }

    return Results.Ok(new { message = "Successfully linked to partner.", primaryName = primaryUser.Name });
}).RequireAuthorization();

// ==========================================
// 3. CYCLE & LOG MANAGEMENT ENDPOINTS
// ==========================================

// GET /api/cycle/current
app.MapGet("/api/cycle/current", async (HttpContext context, DaryaDbContext db, ICycleEngine cycleEngine) =>
{
    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null) return Results.NotFound(new { message = "User not found." });

    User targetUser = user;
    bool isLinked = user.PartnerUserId.HasValue;
    string? partnerName = null;

    if (user.Role == Role.Partner)
    {
        if (!user.PartnerUserId.HasValue)
        {
            return Results.Ok(new { role = user.Role, isLinked = false, message = "Not linked to any cycle tracker." });
        }
        
        var primary = await db.Users.FirstOrDefaultAsync(u => u.Id == user.PartnerUserId.Value);
        if (primary == null)
        {
            return Results.NotFound(new { message = "Linked cycle tracker user not found." });
        }
        targetUser = primary;
        partnerName = primary.Name;
    }
    else // PrimaryUser
    {
        if (user.PartnerUserId.HasValue)
        {
            var partner = await db.Users.FirstOrDefaultAsync(u => u.Id == user.PartnerUserId.Value);
            partnerName = partner?.Name;
        }
    }

    var status = cycleEngine.CalculateStatus(targetUser, DateTime.UtcNow);

    return Results.Ok(new
    {
        userId = targetUser.Id,
        name = user.Name,
        averageCycleLength = targetUser.AverageCycleLength,
        averagePeriodLength = targetUser.AveragePeriodLength,
        lastPeriodStartDate = targetUser.LastPeriodStartDate,
        partnerTelegramChatId = targetUser.PartnerTelegramChatId,
        language = user.Language, // Return authenticated user's UI language preference
        currentDay = status.CurrentDay,
        phase = status.Phase,
        phaseName = status.PhaseName,
        daysUntilNextPeriod = status.DaysUntilNextPeriod,
        partnerAdvice = status.PartnerAdvice,
        isLinked,
        partnerName,
        role = user.Role
    });
}).RequireAuthorization();

// POST /api/logs
app.MapPost("/api/logs", async (DailyLogRequest request, HttpContext context, DaryaDbContext db, ICycleEngine cycleEngine, INotificationService notificationService) =>
{
    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null) return Results.NotFound();

    // Determine target Primary User: if caller is Partner, update their primary partner's logs
    User primaryUser = user;
    if (user.Role == Role.Partner)
    {
        if (!user.PartnerUserId.HasValue)
        {
            return Results.BadRequest(new { message = "Partner is not linked to any user yet." });
        }
        var targetPrimary = await db.Users.FirstOrDefaultAsync(u => u.Id == user.PartnerUserId.Value);
        if (targetPrimary == null)
        {
            return Results.BadRequest(new { message = "Linked primary user could not be found." });
        }
        primaryUser = targetPrimary;
    }

    var logDate = (request.Date ?? DateTime.UtcNow).Date;
    logDate = DateTime.SpecifyKind(logDate, DateTimeKind.Utc);

    var existingLog = await db.DailyLogs
        .FirstOrDefaultAsync(l => l.UserId == primaryUser.Id && l.Date == logDate);

    if (existingLog != null)
    {
        existingLog.FlowIntensity = request.FlowIntensity;
        existingLog.Mood = request.Mood;
        existingLog.PainLevel = request.PainLevel;
        existingLog.Note = request.Note ?? string.Empty;
    }
    else
    {
        var log = new DailyLog
        {
            Id = Guid.NewGuid(),
            UserId = primaryUser.Id,
            Date = logDate,
            FlowIntensity = request.FlowIntensity,
            Mood = request.Mood,
            PainLevel = request.PainLevel,
            Note = request.Note ?? string.Empty
        };
        db.DailyLogs.Add(log);
    }

    await db.SaveChangesAsync();

    // Send Alert across channels (Telegram / Push)
    if (primaryUser.PartnerTelegramChatId.HasValue || primaryUser.PartnerUserId.HasValue)
    {
        var status = cycleEngine.CalculateStatus(primaryUser, DateTime.UtcNow);
        string message = "";
        string title = primaryUser.Language == "fa" 
            ? $"گزارش روزانه {primaryUser.Name}" 
            : $"Darya Daily Update for {primaryUser.Name}";
        string body = "";

        if (primaryUser.Language == "fa")
        {
            string flowName = request.FlowIntensity switch
            {
                FlowIntensity.None => "هیچی",
                FlowIntensity.Light => "کم",
                FlowIntensity.Medium => "متوسط",
                FlowIntensity.Heavy => "زیاد",
                _ => request.FlowIntensity.ToString()
            };

            string moodEmoji = request.Mood switch
            {
                Mood.Calm => "🧘 آرامش",
                Mood.Happy => "☀️ خوشحال",
                Mood.Sensitive => "💖 حساس و احساسی",
                Mood.LowEnergy => "🥱 کم‌انرژی و خسته",
                Mood.Anxious => "💭 مضطرب یا نگران",
                Mood.Irritable => "⚡ کلافه و زودرنج",
                _ => request.Mood.ToString()
            };

            string painStars = new string('★', request.PainLevel) + new string('☆', 5 - request.PainLevel);
            string noteText = string.IsNullOrWhiteSpace(request.Note) ? "_بدون یادداشت_" : $"\"{request.Note}\"";
            string loggerSignature = user.Role == Role.Partner ? $" (ثبت شده توسط همسفر {user.Name})" : "";

            message = 
                $"🌸 *گزارش روزانه {primaryUser.Name}{loggerSignature}* 🌸\n\n" +
                $"*وضعیت دوره:* روز {status.CurrentDay} از {primaryUser.AverageCycleLength} (فاز {TranslatePhaseToFarsi(status.PhaseName)})\n" +
                $"*روزهای مانده تا دوره بعدی:* {status.DaysUntilNextPeriod} روز\n\n" +
                $"*ثبت وضعیت امروز:*\n" +
                $"• حال و هوا: {moodEmoji}\n" +
                $"• میزان درد: {painStars} ({request.PainLevel}/5)\n" +
                $"• شدت خون‌ریزی: {flowName}\n" +
                $"• یادداشت: {noteText}\n\n" +
                $"💡 *پیشنهاد برای همسفرش:*\n" +
                $"{status.PartnerAdvice}";

            body = $"وضعیت: روز {status.CurrentDay} از {primaryUser.AverageCycleLength} (فاز {TranslatePhaseToFarsi(status.PhaseName)}). پیشنهاد: {status.PartnerAdvice}";
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
            string loggerSignature = user.Role == Role.Partner ? $" (Logged by partner {user.Name})" : "";

            message = 
                $"🌸 *Darya Daily Update for {primaryUser.Name}{loggerSignature}* 🌸\n\n" +
                $"*Cycle Status:* Day {status.CurrentDay} of {primaryUser.AverageCycleLength} ({status.PhaseName} Phase)\n" +
                $"*Days until next cycle:* {status.DaysUntilNextPeriod} days\n\n" +
                $"*Today's Check-in:*\n" +
                $"• Mood: {moodEmoji}\n" +
                $"• Pain Level: {painStars} ({request.PainLevel}/5)\n" +
                $"• Flow Intensity: {request.FlowIntensity}\n" +
                $"• Note: {noteText}\n\n" +
                $"💡 *How to support her today:*\n" +
                $"{status.PartnerAdvice}";

            body = $"Cycle Status: Day {status.CurrentDay} of {primaryUser.AverageCycleLength} ({status.PhaseName} Phase). Advice: {status.PartnerAdvice}";
        }

        await notificationService.SendPartnerAlertAsync(primaryUser, new NotificationPayload(title, body, message));
    }

    return Results.Ok(new { message = "Daily log saved successfully." });
}).RequireAuthorization();

// POST /api/user/setup
app.MapPost("/api/user/setup", async (UserSetupRequest request, HttpContext context, DaryaDbContext db, ICycleEngine cycleEngine, INotificationService notificationService) =>
{
    var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdString, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null) return Results.NotFound();

    // Update settings for authenticated user
    user.Name = string.IsNullOrWhiteSpace(request.Name) ? user.Name : request.Name.Trim();
    user.Language = string.IsNullOrWhiteSpace(request.Language) ? user.Language : request.Language;

    // Map targets: Partners can update cycle parameters for the primary tracker if linked
    User targetUser = user;
    if (user.Role == Role.Partner)
    {
        if (!user.PartnerUserId.HasValue)
        {
            return Results.BadRequest(new { message = "Partner is not linked to any user yet." });
        }
        var targetPrimary = await db.Users.FirstOrDefaultAsync(u => u.Id == user.PartnerUserId.Value);
        if (targetPrimary == null)
        {
            return Results.BadRequest(new { message = "Linked primary user could not be found." });
        }
        targetUser = targetPrimary;
    }

    // Apply cycle changes to target user
    if (request.AverageCycleLength >= 20 && request.AverageCycleLength <= 45)
    {
        targetUser.AverageCycleLength = request.AverageCycleLength;
    }

    if (request.AveragePeriodLength >= 2 && request.AveragePeriodLength <= 10)
    {
        targetUser.AveragePeriodLength = request.AveragePeriodLength;
    }

    if (request.LastPeriodStartDate.HasValue)
    {
        // Enforce UTC kind for Npgsql PostgreSQL compatibility
        targetUser.LastPeriodStartDate = DateTime.SpecifyKind(request.LastPeriodStartDate.Value, DateTimeKind.Utc);
    }

    if (request.PartnerTelegramChatId.HasValue)
    {
        targetUser.PartnerTelegramChatId = request.PartnerTelegramChatId.Value;
    }

    await db.SaveChangesAsync();

    // Send Alert via Notification Service if partner exists or chat configured
    if (targetUser.PartnerTelegramChatId.HasValue || targetUser.PartnerUserId.HasValue)
    {
        var status = cycleEngine.CalculateStatus(targetUser, DateTime.UtcNow);
        string message = "";
        string title = targetUser.Language == "fa" 
            ? "تنظیمات اپلیکیشن دریا به‌روزرسانی شد" 
            : "Darya App Configurations Updated!";
        string body = "";

        if (targetUser.Language == "fa")
        {
            message = 
                $"🔔 *تنظیمات اپلیکیشن دریا به‌روزرسانی شد!* 🔔\n\n" +
                $"تنظیمات دوره توسط *{user.Name}* تغییر یافت.\n\n" +
                $"*وضعیت فعلی:* روز {status.CurrentDay} از {targetUser.AverageCycleLength} (فاز {TranslatePhaseToFarsi(status.PhaseName)})\n\n" +
                $"💡 *نکته امروز:*\n" +
                $"{status.PartnerAdvice}";

            body = $"تنظیمات دوره توسط {user.Name} تغییر یافت. وضعیت: روز {status.CurrentDay} از {targetUser.AverageCycleLength}. نکته: {status.PartnerAdvice}";
        }
        else
        {
            message = 
                $"🔔 *Darya App Configurations Updated!* 🔔\n\n" +
                $"Cycle settings were updated by *{user.Name}*.\n\n" +
                $"*Current State:* Day {status.CurrentDay} of {targetUser.AverageCycleLength} ({status.PhaseName} Phase)\n\n" +
                $"💡 *Tip for today:*\n" +
                $"{status.PartnerAdvice}";

            body = $"Cycle settings updated by {user.Name}. State: Day {status.CurrentDay} of {targetUser.AverageCycleLength}. Tip: {status.PartnerAdvice}";
        }

        await notificationService.SendPartnerAlertAsync(targetUser, new NotificationPayload(title, body, message));
    }

    return Results.Ok(new 
    { 
        message = "Settings updated successfully.",
        userId = targetUser.Id
    });
}).RequireAuthorization();

app.MapHealthEndpoints();
app.MapNotificationEndpoints();

app.Run();



// Helper translator
static string TranslatePhaseToFarsi(string phaseName) => phaseName switch
{
    "Menstrual" => "پریود (قاعدگی)",
    "Follicular" => "فولیکولار",
    "Ovulatory" => "تخمک‌گذاری",
    "Luteal" => "لوتئال",
    _ => phaseName
};

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



public record LinkPartnerRequest(
    string Code
);
