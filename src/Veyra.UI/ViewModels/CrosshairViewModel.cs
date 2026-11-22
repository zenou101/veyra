using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class CrosshairViewModel : PageViewModel
{
    private CrosshairSettings _settings = CrosshairSettings.Default;
    private bool _enabled = AppServices.Settings.CrosshairEnabled;
    private string _colour = "#8B7CF6";
    private CrosshairShape _shape = CrosshairShape.CrossWithGap;
    private DisplayInfo? _display;

    public override string Title => "Crosshair";
    public override string Subtitle => "A click-through overlay drawn by Windows. No injection, no hooks, no anti-cheat interaction.";
    public override string Glyph => "\uE7FB";

    public ObservableCollection<CrosshairShape> Shapes { get; } = new(Enum.GetValues<CrosshairShape>());
    public ObservableCollection<string> Colours { get; } = new() { "#8B7CF6", "#3DDC97", "#F5B759", "#F87171", "#FFFFFF", "#000000" };
    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<string> Notes { get; } = new(CrosshairSettings.CompatibilityNotes);

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!Set(ref _enabled, value)) return;
            Apply();
            AppServices.SaveSettings(s => s with { CrosshairEnabled = value });
        }
    }

    public CrosshairShape Shape
    {
        get => _shape;
        set { if (Set(ref _shape, value)) { _settings = _settings with { Shape = value }; Push(); } }
    }

    public string Colour
    {
        get => _colour;
        set { if (Set(ref _colour, value)) { _settings = _settings with { Colour = value }; Push(); } }
    }

    public double Size
    {
        get => _settings.Size;
        set { if (Math.Abs(_settings.Size - value) > 0.01) { _settings = _settings with { Size = value }; OnPropertyChanged(); Push(); } }
    }

    public double Thickness
    {
        get => _settings.Thickness;
        set { if (Math.Abs(_settings.Thickness - value) > 0.01) { _settings = _settings with { Thickness = value }; OnPropertyChanged(); Push(); } }
    }

    public double Gap
    {
        get => _settings.Gap;
        set { if (Math.Abs(_settings.Gap - value) > 0.01) { _settings = _settings with { Gap = value }; OnPropertyChanged(); Push(); } }
    }

    public double Opacity
    {
        get => _settings.Opacity;
        set { if (Math.Abs(_settings.Opacity - value) > 0.01) { _settings = _settings with { Opacity = value }; OnPropertyChanged(); Push(); } }
    }

    public bool Outline
    {
        get => _settings.Outline;
        set { _settings = _settings with { Outline = value }; OnPropertyChanged(); Push(); }
    }

    public DisplayInfo? SelectedDisplay
    {
        get => _display;
        set { if (Set(ref _display, value)) { _display = value; Push(); } }
    }

    public override async System.Threading.Tasks.Task LoadAsync()
    {
        var displays = await AppServices.Displays.GetDisplaysAsync(default);
        Displays.Clear();
        foreach (var display in displays) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();

        _settings = _settings with { Shape = Shape, Colour = Colour };
        if (Enabled) Apply();
    }

    public void Toggle()
    {
        Enabled = !Enabled;
    }

    private void Apply()
    {
        if (Enabled)
        {
            AppServices.Overlay.Show(_settings, SelectedDisplay);
        }
        else
        {
            AppServices.Overlay.Hide();
        }
    }

    private void Push()
    {
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Thickness));
        OnPropertyChanged(nameof(Gap));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(Outline));
        if (Enabled) AppServices.Overlay.Update(_settings);
    }
}
