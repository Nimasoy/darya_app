namespace Darya.Features.Health;

using System.Diagnostics;
using Darya.Server.Data;
using Microsoft.EntityFrameworkCore;

public record GetHealthQuery(bool CheckDependencies = false);

public class GetHealthQueryHandler
{
    private readonly DaryaDbContext _db;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public GetHealthQueryHandler(DaryaDbContext db)
    {
        _db = db;3
    }

    public async Task<HealthReportDto> HandleAsync(GetHealthQuery query, CancellationToken cancellationToken = default)
    {
        var uptime = DateTime.UtcNow - StartTime;
        var memoryAllocatedMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2);

        var report = new HealthReportDto
        {
            Status = "Healthy",
            CheckedAt = DateTime.UtcNow,
            Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
            MemoryAllocatedMb = memoryAllocatedMb,
            Components = new Dictionary<string, ComponentHealthDto>
            {
                ["Api"] = new()
                {
                    Status = "Healthy",
                    LatencyMs = 0,
                    Description = "Darya ASP.NET Core Runtime"
                }
            }
        };

        if (query.CheckDependencies)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                bool canConnect = await _db.Database.CanConnectAsync(cancellationToken);
                sw.Stop();

                if (canConnect)
                {
                    report.Components["Database"] = new ComponentHealthDto
                    {
                        Status = "Healthy",
                        LatencyMs = sw.ElapsedMilliseconds,
                        Description = "PostgreSQL connection verified"
                    };
                }
                else
                {
                    report.Status = "Unhealthy";
                    report.Components["Database"] = new ComponentHealthDto
                    {
                        Status = "Unhealthy",
                        LatencyMs = sw.ElapsedMilliseconds,
                        Description = "PostgreSQL CanConnect returned false"
                    };
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                report.Status = "Unhealthy";
                report.Components["Database"] = new ComponentHealthDto
                {
                    Status = "Unhealthy",
                    LatencyMs = sw.ElapsedMilliseconds,
                    Description = $"PostgreSQL connection failed: {ex.Message}"
                };
            }
        }

        return report;
    }
}
