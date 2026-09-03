namespace Darya.Features.Security;

using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth-policy";
    public const string ApiPolicy = "api-policy";

    public static IServiceCollection AddDaryaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Rate limit threshold exceeded. Please throttle your requests and try again later."
                }, cancellationToken: token);
            };

            // Strict rate limit for authentication endpoints to prevent credential stuffing / brute-force
            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var clientIp = ResolveClientIp(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Standard sliding window rate limit for operational endpoints
            options.AddPolicy(ApiPolicy, httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? ResolveClientIp(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 3,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    });
            });
        });

        return services;
    }

    private static string ResolveClientIp(HttpContext context)
    {
        // Check standard reverse proxy headers (e.g., Cloudflare CF-Connecting-IP, X-Forwarded-For)
        if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp) && !string.IsNullOrWhiteSpace(cfIp))
        {
            return cfIp.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp)) return firstIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }
}
