namespace Veyra.Core.Models;

/// <summary>
/// One telemetry reading. Values are system-wide; per-process values are never presented as system-wide.
/// </summary>
public sealed record TelemetrySample
{
    public required DateTimeOffset Timestamp { get; init; }
    public double? GpuUtilisationPercent { get; init; }
    public double? GpuMemoryUsedMb { get; init; }
    public double? GpuTemperatureC { get; init; }
    public double? CpuUtilisationPercent { get; init; }
    public double? MemoryUsedMb { get; init; }
    public double? MemoryTotalMb { get; init; }
    public string GpuSource { get; init; } = "unavailable";
    public string? GpuEngineDetail { get; init; }
    public bool FpsAvailable => false;

    public static TelemetrySample Empty(DateTimeOffset timestamp) => new() { Timestamp = timestamp };
}

public sealed record TelemetryCapabilities(
    bool GpuUtilisation,
    bool GpuMemory,
    bool GpuTemperature,
    bool Cpu,
    bool Memory,
    string Detail);
