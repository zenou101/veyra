using System;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class OverviewViewModel : PageViewModel
{
    private int _displayCount;
    private string _primaryDisplay = "Checking…";
    private string _colourStatus = "Checking…";
    private string _cpu = "—";
    private string _gpu = "—";
    private string _memory = "—";
    private string _fps = "Unavailable";
    private string _recovery = AppServices.StartupStatus;

    public override string Title => "Overview";
    public override string Subtitle => "Your displays, colour and system state at a glance.";
    public override string Glyph => "\uE80F";

    public int DisplayCount { get => _displayCount; private set => Set(ref _displayCount, value); }
    public string PrimaryDisplay { get => _primaryDisplay; private set => Set(ref _primaryDisplay, value); }
    public string ColourStatus { get => _colourStatus; private set => Set(ref _colourStatus, value); }
    public string Cpu { get => _cpu; private set => Set(ref _cpu, value); }
    public string Gpu { get => _gpu; private set => Set(ref _gpu, value); }
    public string Memory { get => _memory; private set => Set(ref _memory, value); }
    public string Fps { get => _fps; private set => Set(ref _fps, value); }
    public string Recovery { get => _recovery; private set => Set(ref _recovery, value); }

    public string Version => Veyra.Core.AppInfo.Version;
    public string HardwareNote => "Verified on AMD Radeon RX 7800 XT, Windows 10 22H2, including 300 % driver saturation. NVIDIA NVAPI is implemented but not verified on NVIDIA hardware; Intel saturation remains unimplemented. HDR and Windows 11 are not verified.";

    public async Task<double> RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(default);
            DisplayCount = displays.Count;
            var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
            PrimaryDisplay = primary is null ? "No display detected" : $"{primary.FriendlyName} · {primary.ResolutionLabel} · {primary.RefreshLabel}";

            ColourStatus = AppServices.Colour.VendorWritesSupported
                ? "GPU driver colour control available"
                : "Colour ramp active · driver writes unavailable";

            var sample = await AppServices.Telemetry.SampleAsync(default);
            Gpu = sample.GpuUtilisationPercent is null ? "Unavailable" : $"{sample.GpuUtilisationPercent:0} %";
            Cpu = sample.CpuUtilisationPercent is null ? "—" : $"{sample.CpuUtilisationPercent:0} %";
            Memory = sample.MemoryUsedMb is null ? "—" : $"{sample.MemoryUsedMb / 1024:0.0} / {sample.MemoryTotalMb / 1024:0.0} GB";
            Fps = "Unavailable";
            Recovery = AppServices.StartupStatus;
            return sample.GpuUtilisationPercent ?? 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public override Task LoadAsync() => RefreshAsync();
}
