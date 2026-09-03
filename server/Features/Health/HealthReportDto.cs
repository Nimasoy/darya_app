namespace Darya.Features.Health;

public class ComponentHealthDto
{
    public string Status { get; set; } = "Healthy";
    public long LatencyMs { get; set; }
    public string? Description { get; set; }
}

public class HealthReportDto
{
    public string Status { get; set; } = "Healthy";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string Uptime { get; set; } = string.Empty;
    public double MemoryAllocatedMb { get; set; }
    public Dictionary<string, ComponentHealthDto> Components { get; set; } = new();
}
