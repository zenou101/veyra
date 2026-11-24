using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class BoostViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private double _darkBoost;
    private double _zoomPercent = AppServices.Settings.UiScale * 100;
    private bool _suppress;
    private CancellationTokenSource? _timer;
    private string _status = "Nothing is applied until you move a slider.";

    public override string Title => "Dark Boost & Zoom";
    public override string Subtitle => "Deepen dark scenes without losing colour, and scale Veyra's own interface.";
    public override string Glyph => "\uE706";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set { if (Set(ref _selectedDisplay, value)) _ = ReadCurrentAsync(); }
    }

    public double DarkBoost
    {
        get => _darkBoost;
        set
        {
            if (!Set(ref _darkBoost, value)) return;
            OnPropertyChanged(nameof(DarkBoostDetail));
            if (!_suppress) ScheduleDarkBoost();
        }
    }

    public string DarkBoostDetail => $"{DarkBoost:0} %";
    public double ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            if (!Set(ref _zoomPercent, value)) return;
            OnPropertyChanged(nameof(ZoomDetail));
            AppServices.SaveSettings(s => s with { UiScale = Math.Clamp(value / 100.0, 0.8, 1.25) });
            AppearanceService.Apply();
            Status = $"Interface zoom set to {value:0} %.";
        }
    }

    public string ZoomDetail => $"{ZoomPercent:0} %";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public override async Task LoadAsync()
    {
        var displays = await AppServices.Displays.GetDisplaysAsync(default);
        Displays.Clear();
        foreach (var display in displays) Displays.Add(display);
        SelectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
    }

    private async Task ReadCurrentAsync()
    {
        if (SelectedDisplay is null) return;

        var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);
        _suppress = true;
        try
        {
            DarkBoost = Math.Clamp((state.Values.BlackLevel ?? 0) * 2, 0, 100);
        }
        finally
        {
            _suppress = false;
        }
    }

    private void ScheduleDarkBoost()
    {
        if (SelectedDisplay is null) return;

        _timer?.Cancel();
        var cts = new CancellationTokenSource();
        _timer = cts;
        _ = ApplyAfterDelayAsync(cts.Token);
    }

    private async Task ApplyAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (SelectedDisplay is null || cancellationToken.IsCancellationRequested) return;

        var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);
        var settings = state.Values with
        {
            BlackLevel = DarkBoost * 0.5,
            Shadow = -DarkBoost * 0.35
        };

        var result = await AppServices.Colour.ApplyRampAsync(SelectedDisplay, settings, CancellationToken.None);
        Status = result.Message + $" Dark boost {DarkBoost:0} %.";
    }

    public async Task ResetDarkBoostAsync()
    {
        DarkBoost = 0;
        await Task.CompletedTask;
    }

    public void ResetZoom()
    {
        ZoomPercent = 100;
    }
}
