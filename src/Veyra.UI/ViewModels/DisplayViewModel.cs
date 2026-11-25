using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class DisplayViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private string _status = "Reading the driver's real capabilities…";
    private double _brightness;
    private double _contrast;
    private double _gamma = 1;
    private double _temperature = 6500;
    private double _saturation = 100;
    private double _saturationMaximum = SaturationScale.MaximumPercent;
    private double _shadow;
    private double _blackLevel;
    private double _redTint;
    private string _saturationProviderLabel = "DRIVER";
    private bool _saturationOn = true;
    private bool _saturationEnabled;
    private bool _suppressLive;
    private CancellationTokenSource? _live;
    private CancellationTokenSource? _rampLive;
    private string _saturationReason = string.Empty;
    private string _presetName = string.Empty;
    private int _presetIndex = -1;

    public override string Title => "Display";
    public override string Subtitle => "Tune the colour, then save it as a look you can reuse.";
    public override string Glyph => "\uE7F4";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<PresetCardViewModel> Presets { get; } = new();
    public bool PresetsEmpty => Presets.Count == 0;

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (!Set(ref _selectedDisplay, value) || value is null) return;
            _ = LoadForDisplayAsync();
        }
    }

    public string DisplayName => SelectedDisplay?.FriendlyName ?? "No display";
    public string DisplayMeta => SelectedDisplay is null
        ? "Waiting for Windows"
        : $"{SelectedDisplay.ResolutionLabel} · {SelectedDisplay.RefreshLabel} · {(SelectedDisplay.VendorId ?? "Generic adapter")}";
    public string DisplayCount => Displays.Count == 1 ? "1 display" : $"{Displays.Count} displays";

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string PresetName { get => _presetName; set => Set(ref _presetName, value); }

    public double Brightness { get => _brightness; set { if (Set(ref _brightness, value)) ScheduleRampApply(); } }
    public double Contrast { get => _contrast; set { if (Set(ref _contrast, value)) ScheduleRampApply(); } }
    public double Gamma { get => _gamma; set { if (Set(ref _gamma, value)) ScheduleRampApply(); } }
    public double Temperature { get => _temperature; set { if (Set(ref _temperature, value)) ScheduleRampApply(); } }
    public double Shadow { get => _shadow; set { if (Set(ref _shadow, value)) ScheduleRampApply(); } }
    public double BlackLevel { get => _blackLevel; set { if (Set(ref _blackLevel, value)) ScheduleRampApply(); } }
    public double RedTint { get => _redTint; set { if (Set(ref _redTint, value)) ScheduleRampApply(); } }

    /// <summary>0–300 % on AMD, or the NVIDIA driver maximum; 100 is neutral.</summary>
    public double Saturation
    {
        get => _saturation;
        set
        {
            var clamped = Math.Clamp(value, SaturationScale.MinimumPercent, SaturationMaximum);
            if (Set(ref _saturation, clamped)) ScheduleSaturationApply();
        }
    }

    public double SaturationMaximum
    {
        get => _saturationMaximum;
        private set
        {
            if (Set(ref _saturationMaximum, value)) OnPropertyChanged(nameof(SaturationRange));
        }
    }

    public string SaturationRange => SaturationEnabled ? $"0 – {SaturationMaximum:0} %" : "Unavailable";
    public string SaturationProviderLabel { get => _saturationProviderLabel; private set => Set(ref _saturationProviderLabel, value); }

    public bool SaturationOn
    {
        get => _saturationOn;
        set
        {
            if (Set(ref _saturationOn, value) && !_suppressLive) _ = ToggleSaturationAsync();
        }
    }

    public bool SaturationEnabled
    {
        get => _saturationEnabled;
        private set
        {
            if (Set(ref _saturationEnabled, value)) OnPropertyChanged(nameof(SaturationRange));
        }
    }

    public string SaturationReason { get => _saturationReason; private set => Set(ref _saturationReason, value); }

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(default);
            Displays.Clear();
            foreach (var display in displays) Displays.Add(display);

            await ReloadPresetsAsync();

            var current = SelectedDisplay;
            SelectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
            if (SelectedDisplay is null)
            {
                Status = "No connected display was detected. Colour controls need an active display.";
            }
            else if (ReferenceEquals(current, SelectedDisplay))
            {
                await LoadForDisplayAsync();
            }

            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(DisplayMeta));
            OnPropertyChanged(nameof(DisplayCount));
        }
        catch (Exception ex)
        {
            Status = "Colour loading failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadForDisplayAsync()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DisplayMeta));
        if (SelectedDisplay is null) return;

        _live?.Cancel();
        _rampLive?.Cancel();
        IsBusy = true;
        try
        {
            var capabilities = await AppServices.Colour.GetCapabilitiesAsync(SelectedDisplay, default);

            var saturation = capabilities.Controls.FirstOrDefault(c => c.Control == ColourControl.Saturation);
            SaturationProviderLabel = saturation?.Source.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) == true
                ? "NVIDIA DRIVER"
                : saturation?.Source.Contains("AMD", StringComparison.OrdinalIgnoreCase) == true
                    ? "AMD DRIVER"
                    : saturation?.IsSupported == true ? "DRIVER" : "NO DRIVER";
            SaturationReason = saturation is null
                ? "Saturation is not available from this provider."
                : saturation.IsSupported
                    ? BuildSaturationReason(saturation)
                    : saturation.Reason;

            var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);

            // Loading captured state must not immediately re-apply it. The saturation
            // maximum can also coerce the slider and write back to Saturation.
            _suppressLive = true;
            try
            {
                Brightness = state.Values.Brightness ?? 0;
                Contrast = state.Values.Contrast ?? 0;
                Gamma = state.Values.Gamma ?? 1;
                Temperature = state.Values.Temperature ?? 6500;
                Shadow = state.Values.Shadow ?? 0;
                BlackLevel = state.Values.BlackLevel ?? 0;
                RedTint = state.Values.Red ?? 0;
                SaturationEnabled = saturation?.IsSupported == true;
                SaturationMaximum = saturation is { IsSupported: true }
                    ? saturation.Maximum
                    : SaturationScale.MaximumPercent;
                Saturation = state.Values.Saturation ?? 100;
                SaturationOn = true;
            }
            finally
            {
                _suppressLive = false;
            }

            Status = capabilities.Summary ?? "Capabilities loaded.";
        }
        catch (Exception ex)
        {
            Status = "Capability query failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ColourSettings CurrentSettings() => new()
    {
        Brightness = Brightness,
        Contrast = Contrast,
        Gamma = Gamma,
        Temperature = Temperature,
        Shadow = Shadow,
        BlackLevel = BlackLevel,
        Red = RedTint,
        Saturation = SaturationOn ? Saturation : 100
    };

    public async Task ApplyPresetAsync(PresetCardViewModel card)
    {
        if (SelectedDisplay is null) return;

        IsBusy = true;
        try
        {
            _live?.Cancel();
            _rampLive?.Cancel();
            var preset = card.Preset;
            var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, preset.Colour, default);
            Status = result.IsSuccess ? $"{preset.Name}: {result.Message}" : result.Message;

            _suppressLive = true;
            try
            {
                Brightness = preset.Colour.Brightness ?? 0;
                Contrast = preset.Colour.Contrast ?? 0;
                Gamma = preset.Colour.Gamma ?? 1;
                Temperature = preset.Colour.Temperature ?? 6500;
                Shadow = preset.Colour.Shadow ?? 0;
                BlackLevel = preset.Colour.BlackLevel ?? 0;
                RedTint = preset.Colour.Red ?? 0;
                Saturation = preset.Colour.Saturation ?? 100;
                SaturationOn = true;
            }
            finally
            {
                _suppressLive = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeletePresetAsync(PresetCardViewModel card)
    {
        if (card is null) return;

        await AppServices.Presets.DeleteAsync(card.Id, default);
        await ReloadPresetsAsync();
        Status = $"Look '{card.Name}' deleted.";
    }

    public async Task CyclePresetAsync()
    {
        if (Presets.Count == 0) return;
        _presetIndex = (_presetIndex + 1) % Presets.Count;
        await ApplyPresetAsync(Presets[_presetIndex]);
    }

    public async Task SavePresetAsync()
    {
        var name = PresetName.Trim();
        if (name.Length == 0)
        {
            Status = "Enter a look name first.";
            return;
        }

        if (name.Length > 40) name = name[..40];

        var preset = new Preset
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            Name = name,
            Description = "Saved from the Display page.",
            Kind = PresetKind.Colour,
            Colour = CurrentSettings()
        };

        await AppServices.Presets.SaveAsync(preset, default);
        await ReloadPresetsAsync();
        PresetName = string.Empty;
        Status = $"Look '{preset.Name}' saved.";
    }

    public async Task ResetAsync()
    {
        if (SelectedDisplay is null) return;

        _live?.Cancel();
        _rampLive?.Cancel();
        IsBusy = true;
        try
        {
            var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, ColourSettings.Neutral, default);

            _suppressLive = true;
            try
            {
                Brightness = 0;
                Contrast = 0;
                Gamma = 1;
                Temperature = 6500;
                Shadow = 0;
                BlackLevel = 0;
                RedTint = 0;
                Saturation = 100;
                SaturationOn = true;
            }
            finally
            {
                _suppressLive = false;
            }

            Status = result.IsSuccess ? "Neutral look applied. " + result.Message : result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RestoreAsync()
    {
        if (SelectedDisplay is null) return;

        _live?.Cancel();
        _rampLive?.Cancel();
        IsBusy = true;
        try
        {
            var result = await AppServices.Colour.RestoreAsync(SelectedDisplay, default);
            await LoadForDisplayAsync();
            Status = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadPresetsAsync()
    {
        var presets = await AppServices.Presets.GetAllAsync(default);
        Presets.Clear();

        foreach (var preset in presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Presets.Add(new PresetCardViewModel(preset));
        }

        OnPropertyChanged(nameof(PresetsEmpty));
    }

    private static string BuildSaturationReason(ColourControlCapability saturation)
    {
        var isNvidia = saturation.Source.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
        var driverDescription = isNvidia
            ? $"Per-display NVIDIA digital vibrance through NVAPI, not a gamma curve. 100 % is neutral and {saturation.Maximum:0} % is this driver's maximum. "
            : "Per-display AMD driver saturation, not a gamma curve. 100 % is neutral, 200 % is the driver's advertised maximum, and 300 % is the verified extended ceiling. ";

        return driverDescription +
               "The exact driver value is captured before the first change and restored by the toggle, Restore original, or after a crash.";
    }

    private void ScheduleRampApply()
    {
        if (_suppressLive || SelectedDisplay is null) return;

        _rampLive?.Cancel();
        var cts = new CancellationTokenSource();
        _rampLive = cts;
        _ = ApplyRampAfterDelayAsync(cts.Token);
    }

    private async Task ApplyRampAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(140, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var display = SelectedDisplay;
        if (cancellationToken.IsCancellationRequested || display is null) return;

        var result = await AppServices.Colour.ApplyRampAsync(display, CurrentSettings(), CancellationToken.None);
        Status = result.Message;
    }

    private void ScheduleSaturationApply()
    {
        if (_suppressLive || !SaturationEnabled || !SaturationOn || SelectedDisplay is null) return;

        _live?.Cancel();
        var cts = new CancellationTokenSource();
        _live = cts;
        _ = ApplySaturationAfterDelayAsync(cts.Token);
    }

    private async Task ApplySaturationAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(140, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var display = SelectedDisplay;
        if (cancellationToken.IsCancellationRequested || display is null) return;

        var result = await AppServices.Colour.ApplySaturationAsync(
            display, Saturation, CancellationToken.None);
        Status = result.Message;
    }

    private async Task ToggleSaturationAsync()
    {
        if (SelectedDisplay is null || !SaturationEnabled) return;

        _live?.Cancel();

        var result = SaturationOn
            ? await AppServices.Colour.ApplySaturationAsync(SelectedDisplay, Saturation, CancellationToken.None)
            : await AppServices.Colour.RestoreSaturationAsync(SelectedDisplay, CancellationToken.None);

        Status = result.Message;
    }
}
