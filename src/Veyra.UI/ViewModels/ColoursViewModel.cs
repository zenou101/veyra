using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class ColourControlRow : ObservableObject
{
    public ColourControlRow(ColourControlCapability capability)
    {
        Capability = capability;
    }

    public ColourControlCapability Capability { get; }
    public string Name => Capability.DisplayName;
    public bool IsSupported => Capability.IsSupported;
    public string Range => Capability.IsSupported
        ? $"{Capability.Minimum:0.##} – {Capability.Maximum:0.##} {Capability.Unit}".Trim()
        : "Unavailable";
    public string Reason => Capability.IsSupported ? Capability.Source : Capability.Reason;
}

public sealed class ColoursViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private string _status = "Reading the driver's real capabilities…";
    private double _brightness;
    private double _contrast;
    private double _gamma = 1;
    private double _temperature = 6500;
    private double _saturation = 100;
    private double _saturationMaximum = SaturationScale.MaximumPercent;
    private string _saturationProviderLabel = "DRIVER";
    private bool _saturationOn = true;
    private bool _saturationEnabled;
    private bool _suppressLive;
    private CancellationTokenSource? _live;
    private CancellationTokenSource? _rampLive;
    private string _saturationReason = string.Empty;
    private string _presetName = string.Empty;
    private Preset? _selectedPreset;

    public override string Title => "Colours";
    public override string Subtitle => "Real AMD or NVIDIA driver saturation plus Windows ramp controls. One click restores the original colours.";
    public override string Glyph => "\uE790";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<ColourControlRow> Controls { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (Set(ref _selectedDisplay, value)) _ = LoadForDisplayAsync();
        }
    }

    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (Set(ref _selectedPreset, value)) OnPropertyChanged(nameof(CanDeletePreset));
        }
    }

    public bool CanDeletePreset => SelectedPreset is { IsBuiltIn: false };
    public string PresetName { get => _presetName; set => Set(ref _presetName, value); }

    public string Status { get => _status; private set => Set(ref _status, value); }
    public double Brightness { get => _brightness; set { if (Set(ref _brightness, value)) ScheduleRampApply(); } }
    public double Contrast { get => _contrast; set { if (Set(ref _contrast, value)) ScheduleRampApply(); } }
    public double Gamma { get => _gamma; set { if (Set(ref _gamma, value)) ScheduleRampApply(); } }
    public double Temperature { get => _temperature; set { if (Set(ref _temperature, value)) ScheduleRampApply(); } }

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

            Presets.Clear();
            foreach (var preset in await AppServices.Presets.GetAllAsync(default)) Presets.Add(preset);

            SelectedDisplay = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();
            await LoadForDisplayAsync();
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
        if (SelectedDisplay is null) return;

        _live?.Cancel();
        _rampLive?.Cancel();
        IsBusy = true;
        try
        {
            var capabilities = await AppServices.Colour.GetCapabilitiesAsync(SelectedDisplay, default);
            Controls.Clear();
            foreach (var control in capabilities.Controls) Controls.Add(new ColourControlRow(control));

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

            // Loading the captured state must not immediately re-apply it. The saturation
            // maximum can also coerce the slider and write back to Saturation, so suppress
            // all live writes until every loaded value is in place.
            _suppressLive = true;
            try
            {
                Brightness = state.Values.Brightness ?? 0;
                Contrast = state.Values.Contrast ?? 0;
                Gamma = state.Values.Gamma ?? 1;
                Temperature = state.Values.Temperature ?? 6500;
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

    public async Task ApplyAsync()
    {
        if (SelectedDisplay is null) return;

        _live?.Cancel();
        _rampLive?.Cancel();
        IsBusy = true;
        try
        {
            var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, CurrentSettings(), default);
            Status = result.Message;
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
        Saturation = SaturationOn ? Saturation : 100
    };

    public async Task ApplyPresetAsync()
    {
        if (SelectedDisplay is null || SelectedPreset is null) return;

        IsBusy = true;
        try
        {
            _live?.Cancel();
            _rampLive?.Cancel();
            var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, SelectedPreset.Colour, default);
            Status = result.IsSuccess ? $"{SelectedPreset.Name}: {result.Message}" : result.Message;

            _suppressLive = true;
            try
            {
                Brightness = SelectedPreset.Colour.Brightness ?? 0;
                Contrast = SelectedPreset.Colour.Contrast ?? 0;
                Gamma = SelectedPreset.Colour.Gamma ?? 1;
                Temperature = SelectedPreset.Colour.Temperature ?? 6500;
                Saturation = SelectedPreset.Colour.Saturation ?? 100;
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

    public async Task SavePresetAsync()
    {
        var name = PresetName.Trim();
        if (name.Length == 0)
        {
            Status = "Enter a preset name first.";
            return;
        }

        if (name.Length > 40) name = name[..40];

        var preset = new Preset
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            Name = name,
            Description = "Saved from the Colours page.",
            Kind = PresetKind.Colour,
            Colour = new ColourSettings
            {
                Brightness = Brightness,
                Contrast = Contrast,
                Gamma = Gamma,
                Temperature = Temperature,
                Saturation = SaturationOn ? Saturation : 100
            }
        };

        await AppServices.Presets.SaveAsync(preset, default);
        await ReloadPresetsAsync();
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == preset.Id);
        PresetName = string.Empty;
        Status = $"Preset '{preset.Name}' saved.";
    }

    public async Task DeletePresetAsync()
    {
        if (SelectedPreset is not { IsBuiltIn: false } preset)
        {
            Status = "Built-in presets cannot be deleted.";
            return;
        }

        await AppServices.Presets.DeleteAsync(preset.Id, default);
        await ReloadPresetsAsync();
        Status = $"Preset '{preset.Name}' deleted.";
    }

    private async Task ReloadPresetsAsync()
    {
        var presets = await AppServices.Presets.GetAllAsync(default);
        Presets.Clear();
        foreach (var preset in presets) Presets.Add(preset);
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

    private static string BuildSaturationReason(ColourControlCapability saturation)
    {
        var isNvidia = saturation.Source.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
        var driverDescription = isNvidia
            ? $"Per-display NVIDIA digital vibrance through NVAPI, not a gamma curve. 100 % is neutral and {saturation.Maximum:0} % is this driver's maximum; NVIDIA does not expose AMD's extended 300 % range. "
            : "Per-display AMD driver saturation, not a gamma curve. 100 % is neutral, 200 % is the driver's advertised maximum, and 300 % is the verified extended ceiling. ";

        return driverDescription +
               "The exact driver value is captured before the first change and restored by the toggle, Restore original, or after a crash. Brightness and gamma stay on the Windows ramp.";
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
