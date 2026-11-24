using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;
using Veyra.UI.Services;

namespace Veyra.UI.ViewModels;

public sealed class MonitorViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private string _status = "Reading the monitor's DDC controls…";
    private bool _hasBrightness;
    private bool _hasContrast;
    private double _brightness = 50;
    private double _contrast = 50;
    private double _originalBrightness = 50;
    private double _originalContrast = 50;
    private double _gammaPercent = 100;
    private double _warmth;
    private bool _lowBlueLight;
    private bool _suppress;
    private CancellationTokenSource? _ddcTimer;
    private CancellationTokenSource? _rampTimer;

    public override string Title => "Monitor";
    public override string Subtitle => "Your monitor's own settings, without reaching round the back.";
    public override string Glyph => "\uE7F8";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (!Set(ref _selectedDisplay, value) || value is null) return;
            _ = LoadMonitorAsync();
        }
    }

    public string MonitorName => SelectedDisplay?.FriendlyName ?? "No monitor";
    public string MonitorDetail => SelectedDisplay is null
        ? "Waiting for Windows"
        : $"{SelectedDisplay.ResolutionLabel} · {SelectedDisplay.RefreshLabel} · {SelectedDisplay.AdapterName}";
    public bool HasBrightness { get => _hasBrightness; private set => Set(ref _hasBrightness, value); }
    public bool HasContrast { get => _hasContrast; private set => Set(ref _hasContrast, value); }
    public bool DdcAvailable => HasBrightness || HasContrast;
    public string DdcDetail => DdcAvailable
        ? "Brightness and contrast are sent to the monitor over DDC/CI. Other apps can read the same controls."
        : "This monitor did not expose DDC/CI brightness or contrast. The Windows colour controls below still work.";

    public double Brightness
    {
        get => _brightness;
        set
        {
            if (!Set(ref _brightness, value) || _suppress) return;
            ScheduleDdc(false);
        }
    }

    public double Contrast
    {
        get => _contrast;
        set
        {
            if (!Set(ref _contrast, value) || _suppress) return;
            ScheduleDdc(true);
        }
    }

    public double GammaPercent
    {
        get => _gammaPercent;
        set { if (Set(ref _gammaPercent, value)) ScheduleRamp(); }
    }

    public double Warmth
    {
        get => _warmth;
        set
        {
            if (!Set(ref _warmth, value)) return;
            OnPropertyChanged(nameof(WarmthDetail));
            ScheduleRamp();
        }
    }

    public bool LowBlueLight
    {
        get => _lowBlueLight;
        set
        {
            if (!Set(ref _lowBlueLight, value) || _suppress) return;
            Warmth = value ? 65 : 0;
        }
    }

    public string WarmthDetail => $"{Kelvin:0} K";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private double Kelvin => 6500 - Math.Clamp(Warmth, 0, 100) * 30;
    private double Gamma => 0.7 + (Math.Clamp(GammaPercent, 50, 150) - 50) / 100.0 * 0.6;

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(default);
            Displays.Clear();
            foreach (var display in displays) Displays.Add(display);
            SelectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
            if (SelectedDisplay is null) Status = "No connected display was detected.";
        }
        catch (Exception ex)
        {
            Status = "Monitor enumeration failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadMonitorAsync()
    {
        OnPropertyChanged(nameof(MonitorName));
        OnPropertyChanged(nameof(MonitorDetail));
        if (SelectedDisplay is null) return;

        using var ddc = MonitorDdc.TryOpen(SelectedDisplay);
        _suppress = true;
        try
        {
            HasBrightness = ddc?.HasBrightness == true;
            HasContrast = ddc?.HasContrast == true;
            Brightness = ddc?.BrightnessPercent ?? 50;
            Contrast = ddc?.ContrastPercent ?? 50;
            _originalBrightness = Brightness;
            _originalContrast = Contrast;
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(DdcAvailable));
        OnPropertyChanged(nameof(DdcDetail));

        var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);
        _suppress = true;
        try
        {
            GammaPercent = state.Values.Gamma is { } gamma
                ? Math.Clamp((gamma - 0.7) / 0.6 * 100 + 50, 50, 150)
                : 100;
            Warmth = state.Values.Temperature is { } temperature
                ? Math.Clamp((6500 - temperature) / 30, 0, 100)
                : 0;
            LowBlueLight = Warmth >= 55;
        }
        finally
        {
            _suppress = false;
        }

        Status = DdcAvailable
            ? $"{MonitorName}: hardware brightness and contrast are available."
            : "DDC/CI is not exposed by this monitor. Colour controls still work.";
    }

    private void ScheduleDdc(bool contrast)
    {
        if (!DdcAvailable || SelectedDisplay is null || _suppress) return;

        _ddcTimer?.Cancel();
        var cts = new CancellationTokenSource();
        _ddcTimer = cts;
        _ = ApplyDdcAfterDelayAsync(contrast, cts.Token);
    }

    private async Task ApplyDdcAfterDelayAsync(bool contrast, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(220, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (SelectedDisplay is null || cancellationToken.IsCancellationRequested) return;

        using var ddc = MonitorDdc.TryOpen(SelectedDisplay);
        if (ddc is null)
        {
            Status = "The monitor's DDC handle could not be opened.";
            return;
        }

        var ok = contrast ? ddc.SetContrastPercent(Contrast) : ddc.SetBrightnessPercent(Brightness);
        Status = ok
            ? contrast ? $"Contrast set to {Contrast:0} %." : $"Brightness set to {Brightness:0} %."
            : "The monitor refused that DDC write.";
    }

    private void ScheduleRamp()
    {
        if (_suppress || SelectedDisplay is null) return;

        _rampTimer?.Cancel();
        var cts = new CancellationTokenSource();
        _rampTimer = cts;
        _ = ApplyRampAfterDelayAsync(cts.Token);
    }

    private async Task ApplyRampAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(160, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (SelectedDisplay is null || cancellationToken.IsCancellationRequested) return;

        var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);
        var settings = state.Values with { Temperature = Kelvin, Gamma = Gamma };
        var result = await AppServices.Colour.ApplyRampAsync(SelectedDisplay, settings, CancellationToken.None);
        Status = result.Message + $" · {Kelvin:0} K";
    }

    public async Task RestoreAsync()
    {
        if (SelectedDisplay is null) return;

        _suppress = true;
        try
        {
            Brightness = _originalBrightness;
            Contrast = _originalContrast;
            Warmth = 0;
            GammaPercent = 100;
            LowBlueLight = false;
        }
        finally
        {
            _suppress = false;
        }

        using (var ddc = MonitorDdc.TryOpen(SelectedDisplay))
        {
            if (ddc is not null)
            {
                if (ddc.HasBrightness) ddc.SetBrightnessPercent(_originalBrightness);
                if (ddc.HasContrast) ddc.SetContrastPercent(_originalContrast);
            }
        }

        var result = await AppServices.Colour.RestoreAsync(SelectedDisplay, default);
        Status = "Monitor and colour settings restored. " + result.Message;
    }
}
