using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class ResolutionOption
{
    public ResolutionOption(int width, int height, bool isCurrent)
    {
        Width = width;
        Height = height;
        IsCurrent = isCurrent;
    }

    public int Width { get; }
    public int Height { get; }
    public bool IsCurrent { get; }
    public string Label => $"{Width} × {Height}";
    public string Aspect => AspectLabel(Width, Height);
    public string Megapixels => $"{Width * (double)Height / 1_000_000:0.00} MP";

    private static string AspectLabel(int width, int height)
    {
        var divisor = Gcd(width, height);
        return divisor == 0 ? "—" : $"{width / divisor}:{height / divisor}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            var t = b;
            b = a % b;
            a = t;
        }
        return a;
    }
}

public sealed class ResolutionViewModel : PageViewModel
{
    private readonly List<DisplayMode> _modes = new();
    private DisplayInfo? _selectedDisplay;
    private ResolutionOption? _selectedResolution;
    private int _selectedRefresh;
    private string _status = "Every change shows a 15-second confirmation and rolls back by itself.";

    public override string Title => "Resolution";
    public override string Subtitle => "Change resolution and refresh rate without leaving Veyra.";
    public override string Glyph => "\uE740";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<ResolutionOption> Resolutions { get; } = new();
    public ObservableCollection<int> RefreshRates { get; } = new();

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (!Set(ref _selectedDisplay, value) || value is null) return;
            _ = LoadModesAsync();
        }
    }

    public ResolutionOption? SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (!Set(ref _selectedResolution, value) || value is null) return;
            RebuildRefreshRates();
            OnPropertyChanged(nameof(AspectLabel));
            OnPropertyChanged(nameof(Megapixels));
            OnPropertyChanged(nameof(SelectionLabel));
        }
    }

    public int SelectedRefresh
    {
        get => _selectedRefresh;
        set { if (Set(ref _selectedRefresh, value)) OnPropertyChanged(nameof(SelectionLabel)); }
    }

    public string CurrentResolution => SelectedDisplay is null ? "—" : $"{SelectedDisplay.Width} × {SelectedDisplay.Height}";
    public string CurrentRefresh => SelectedDisplay is null ? "—" : $"{SelectedDisplay.RefreshRate} Hz";
    public string CurrentDetail => SelectedDisplay is null
        ? "No display detected"
        : $"{SelectedDisplay.RefreshLabel} · {SelectedDisplay.Orientation} · {SelectedDisplay.BitsPerPixel}-bit";
    public string AspectLabel => SelectedResolution?.Aspect ?? (SelectedDisplay is null ? "—" : AspectOf(SelectedDisplay.Width, SelectedDisplay.Height));
    public string Megapixels => SelectedResolution?.Megapixels ?? "—";
    public string ModeCount => $"{Resolutions.Count} resolution{(Resolutions.Count == 1 ? string.Empty : "s")} · {_modes.Count} modes";
    public string SelectionLabel => SelectedResolution is null || SelectedRefresh == 0
        ? "Choose a resolution and refresh rate."
        : $"{SelectedResolution.Label} at {SelectedRefresh} Hz";
    public string Status { get => _status; private set => Set(ref _status, value); }

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
            Status = "Display enumeration failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadModesAsync()
    {
        if (SelectedDisplay is null) return;

        OnPropertyChanged(nameof(CurrentResolution));
        OnPropertyChanged(nameof(CurrentRefresh));
        OnPropertyChanged(nameof(CurrentDetail));
        OnPropertyChanged(nameof(AspectLabel));

        IsBusy = true;
        try
        {
            var modes = await AppServices.Displays.GetModesAsync(SelectedDisplay.Id, default);
            _modes.Clear();
            _modes.AddRange(modes.OrderByDescending(m => m.Width * m.Height).ThenByDescending(m => m.RefreshRate));

            Resolutions.Clear();
            foreach (var group in _modes.GroupBy(m => (m.Width, m.Height))
                         .OrderByDescending(g => g.Key.Width * g.Key.Height))
            {
                Resolutions.Add(new ResolutionOption(group.Key.Width, group.Key.Height,
                    group.Key.Width == SelectedDisplay.Width && group.Key.Height == SelectedDisplay.Height));
            }

            var current = Resolutions.FirstOrDefault(r => r.IsCurrent) ?? Resolutions.FirstOrDefault();
            SelectedResolution = current;
            OnPropertyChanged(nameof(ModeCount));
            Status = _modes.Count == 0
                ? "Windows returned no modes for this display."
                : $"{_modes.Count} modes read from Windows. Highest refresh first.";
        }
        catch (Exception ex)
        {
            Status = "Mode enumeration failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildRefreshRates()
    {
        RefreshRates.Clear();
        if (SelectedResolution is null) return;

        var rates = _modes
            .Where(m => m.Width == SelectedResolution.Width && m.Height == SelectedResolution.Height)
            .Select(m => m.RefreshRate)
            .Distinct()
            .OrderByDescending(r => r);

        foreach (var rate in rates) RefreshRates.Add(rate);

        var current = SelectedDisplay?.RefreshRate ?? 0;
        SelectedRefresh = RefreshRates.Contains(current) ? current : RefreshRates.FirstOrDefault();
    }

    public void SelectHighestRefresh()
    {
        if (RefreshRates.Count > 0) SelectedRefresh = RefreshRates[0];
    }

    public void SelectClosestRefresh(int target)
    {
        if (RefreshRates.Count == 0) return;
        SelectedRefresh = RefreshRates.OrderBy(r => Math.Abs(r - target)).First();
    }

    public DisplayMode? BuildSelectedMode()
    {
        if (SelectedDisplay is null || SelectedResolution is null || SelectedRefresh == 0) return null;

        return new DisplayMode(SelectedResolution.Width, SelectedResolution.Height, SelectedRefresh,
            SelectedDisplay.BitsPerPixel, (int)SelectedDisplay.Orientation);
    }

    public bool IsSelectedModeCurrent()
    {
        return SelectedDisplay is not null &&
               SelectedResolution is not null &&
               SelectedResolution.Width == SelectedDisplay.Width &&
               SelectedResolution.Height == SelectedDisplay.Height &&
               SelectedRefresh == SelectedDisplay.RefreshRate;
    }

    public void ReportStatus(string message) => Status = message;

    private static string AspectOf(int width, int height)
    {
        if (width <= 0 || height <= 0) return "—";
        var a = width;
        var b = height;
        while (b != 0)
        {
            var t = b;
            b = a % b;
            a = t;
        }
        return a == 0 ? "—" : $"{width / a}:{height / a}";
    }
}
