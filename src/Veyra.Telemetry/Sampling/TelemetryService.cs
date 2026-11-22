using System.Globalization;
using System.Runtime.InteropServices;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Telemetry.Interop;

namespace Veyra.Telemetry.Sampling;

/// <summary>
/// Samples system-wide GPU utilisation from the GPU Engine counters (3D engines summed),
/// dedicated GPU memory, CPU and RAM. Values are explicitly system-wide, and FPS is
/// reported as unavailable rather than estimated.
/// </summary>
public sealed class TelemetryService : ITelemetryService, IDisposable
{
    private readonly object _sync = new();
    private IntPtr _query;
    private IntPtr _engineCounter;
    private IntPtr _memoryCounter;
    private bool _primed;
    private ulong _lastIdle, _lastKernel, _lastUser;
    private bool _hasCpuBaseline;
    private double _lastCpu;

    public TelemetryCapabilities Capabilities => new(
        GpuUtilisation: GpuCountersAvailable,
        GpuMemory: GpuCountersAvailable,
        GpuTemperature: false,
        Cpu: true,
        Memory: true,
        Detail: "GPU utilisation and dedicated memory are system-wide totals from the GPU Engine and GPU Process Memory counters (all processes). GPU temperature is unavailable: no supported sensor interface answered. FPS is unavailable by design.");

    public string UnavailableDetail =>
        "FPS and frame time are unavailable: Veyra does not inject, hook or read game memory, so no frame data exists.";

    public string GpuDetail =>
        "GPU utilisation is system-wide: the sum of all 3D engine counters across processes, not per-game.";

    public bool GpuCountersAvailable { get; private set; }

    public Task<TelemetrySample> SampleAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            EnsureCounters();

            var gpu = 0d;
            var memory = 0L;

            if (_query != IntPtr.Zero)
            {
                PdhInterop.PdhCollectQueryData(_query);

                if (_primed)
                {
                    gpu = SumInstances(_engineCounter, "engtype_3D", 100d);
                    var bytes = SumInstances(_memoryCounter, null, double.MaxValue);
                    memory = (long)Math.Max(0, bytes);
                }

                _primed = true;
            }

            SystemMetrics.TryReadCpu(out var idle, out var kernel, out var user);
            if (_hasCpuBaseline)
            {
                var idleDelta = (double)(idle - _lastIdle);
                var totalDelta = (double)((kernel - _lastKernel) + (user - _lastUser));
                if (totalDelta > 0)
                {
                    _lastCpu = Math.Clamp((1d - idleDelta / totalDelta) * 100d, 0d, 100d);
                }
            }

            _hasCpuBaseline = true;
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;

            SystemMetrics.TryReadMemory(out var usedGb, out var totalGb, out var ramPercent);

            var sample = new TelemetrySample
            {
                Timestamp = DateTimeOffset.Now,
                GpuUtilisationPercent = GpuCountersAvailable ? Math.Round(gpu, 1) : null,
                GpuMemoryUsedMb = GpuCountersAvailable ? Math.Round(memory / 1024d / 1024d, 0) : null,
                GpuTemperatureC = null,
                CpuUtilisationPercent = Math.Round(_lastCpu, 1),
                MemoryUsedMb = Math.Round(usedGb * 1024d, 0),
                MemoryTotalMb = Math.Round(totalGb * 1024d, 0),
                GpuSource = GpuCountersAvailable ? "PDH GPU Engine counters (system-wide)" : "unavailable",
                GpuEngineDetail = GpuCountersAvailable ? GpuDetail : UnavailableDetail
            };

            return Task.FromResult(sample);
        }
    }

    private void EnsureCounters()
    {
        if (_query != IntPtr.Zero) return;

        if (PdhInterop.PdhOpenQuery(null, IntPtr.Zero, out var query) != PdhInterop.ErrorSuccess)
        {
            GpuCountersAvailable = false;
            return;
        }

        var engine = PdhInterop.PdhAddEnglishCounter(query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out var engineCounter);
        var memory = PdhInterop.PdhAddEnglishCounter(query, @"\GPU Process Memory(*)\Dedicated Usage", IntPtr.Zero, out var memoryCounter);

        _query = query;
        _engineCounter = engineCounter;
        _memoryCounter = memoryCounter;
        GpuCountersAvailable = engine == PdhInterop.ErrorSuccess && memory == PdhInterop.ErrorSuccess;
    }

    private static double SumInstances(IntPtr counter, string? instanceFilter, double cap)
    {
        if (counter == IntPtr.Zero) return 0;

        uint size = 0;
        var status = PdhInterop.PdhGetFormattedCounterArray(counter, PdhInterop.FormatDouble, ref size, out _, IntPtr.Zero);
        if (status != PdhInterop.MoreData || size == 0) return 0;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            status = PdhInterop.PdhGetFormattedCounterArray(counter, PdhInterop.FormatDouble, ref size, out var count, buffer);
            if (status != PdhInterop.ErrorSuccess) return 0;

            var stride = Marshal.SizeOf<PdhInterop.FmtCounterValueItem>();
            var total = 0d;

            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PdhInterop.FmtCounterValueItem>(buffer + i * stride);
                var name = item.Name == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(item.Name) ?? string.Empty;

                if (name.Contains("_Total", StringComparison.OrdinalIgnoreCase)) continue;
                if (instanceFilter is not null && !name.Contains(instanceFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.Value.Status != 0) continue;

                total += Math.Max(0, item.Value.Value);
            }

            return Math.Min(total, cap);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_query != IntPtr.Zero)
            {
                PdhInterop.PdhCloseQuery(_query);
                _query = IntPtr.Zero;
            }
        }
    }
}
