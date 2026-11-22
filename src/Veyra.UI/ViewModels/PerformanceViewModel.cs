using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class PerformanceViewModel : PageViewModel
{
    private CancellationTokenSource? _loop;
    private string _gpu = "—";
    private string _gpuMemory = "—";
    private string _cpu = "—";
    private string _memory = "—";
    private string _temperature = "Unavailable";
    private double _gpuPercent;
    private double _cpuPercent;
    private double _ramPercent;
    private string _source = "Starting…";

    public override string Title => "Performance";
    public override string Subtitle => "System-wide readings from Windows performance counters. FPS is not measured and is never invented.";
    public override string Glyph => "\uE9D9";

    public string Gpu { get => _gpu; private set => Set(ref _gpu, value); }
    public string GpuMemory { get => _gpuMemory; private set => Set(ref _gpuMemory, value); }
    public string Cpu { get => _cpu; private set => Set(ref _cpu, value); }
    public string Memory { get => _memory; private set => Set(ref _memory, value); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string Source { get => _source; private set => Set(ref _source, value); }
    public double GpuPercent { get => _gpuPercent; private set => Set(ref _gpuPercent, value); }
    public double CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }
    public double RamPercent { get => _ramPercent; private set => Set(ref _ramPercent, value); }

    public string FpsText => "Unavailable";
    public string FpsDetail => AppServices.Telemetry.UnavailableDetail;
    public string GpuDetail => AppServices.Telemetry.GpuDetail;

    public override Task LoadAsync()
    {
        _loop?.Cancel();
        _loop = new CancellationTokenSource();
        var token = _loop.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var sample = await AppServices.Telemetry.SampleAsync(token);
                    await Application.Current.Dispatcher.InvokeAsync(() => Apply(sample));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => Source = "Sampling failed: " + ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }, token);

        return Task.CompletedTask;
    }

    private void Apply(TelemetrySample sample)
    {
        Gpu = sample.GpuUtilisationPercent is null ? "Unavailable" : $"{sample.GpuUtilisationPercent:0} %";
        GpuPercent = sample.GpuUtilisationPercent ?? 0;
        GpuMemory = sample.GpuMemoryUsedMb is null ? "Unavailable" : $"{sample.GpuMemoryUsedMb:0} MB";
        Cpu = sample.CpuUtilisationPercent is null ? "—" : $"{sample.CpuUtilisationPercent:0} %";
        CpuPercent = sample.CpuUtilisationPercent ?? 0;
        Memory = sample.MemoryUsedMb is null ? "—" : $"{sample.MemoryUsedMb / 1024:0.0} of {sample.MemoryTotalMb / 1024:0.0} GB";
        RamPercent = sample.MemoryTotalMb is > 0 ? (sample.MemoryUsedMb ?? 0) / sample.MemoryTotalMb.Value * 100 : 0;
        Temperature = sample.GpuTemperatureC is null ? "Unavailable" : $"{sample.GpuTemperatureC:0} °C";
        Source = sample.GpuSource;
    }

    public void Stop() => _loop?.Cancel();
}
