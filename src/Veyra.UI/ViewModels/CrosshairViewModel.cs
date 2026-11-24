using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class CrosshairStyleOption
{
    public CrosshairStyleOption(string name, string description, CrosshairSettings settings)
    {
        Name = name;
        Description = description;
        Settings = settings;
    }

    public string Name { get; }
    public string Description { get; }
    public CrosshairSettings Settings { get; }
}

public sealed class CrosshairViewModel : PageViewModel
{
    private CrosshairSettings _settings = CrosshairSettings.Default;
    private bool _enabled = AppServices.Settings.CrosshairEnabled;
    private string _colour = "#8B7CF6";
    private CrosshairShape _shape = CrosshairShape.CrossWithGap;
    private DisplayInfo? _display;
    private CrosshairStyleOption? _selectedStyle;

    public override string Title => "Crosshair";
    public override string Subtitle => "A click-through overlay drawn by Windows. No injection, no hooks, no anti-cheat interaction.";
    public override string Glyph => "\uE7FB";

    public ObservableCollection<CrosshairShape> Shapes { get; } = new(Enum.GetValues<CrosshairShape>());
    public ObservableCollection<string> Colours { get; } = new() { "#8B7CF6", "#3DDC97", "#F5B759", "#F87171", "#FFFFFF", "#000000" };
    public ObservableCollection<CrosshairStyleOption> Styles { get; } = new()
    {
        new("Small Static", "A compact dot for precision.",
            new CrosshairSettings { Shape = CrosshairShape.Dot, Size = 6, Thickness = 2, Gap = 0 }),
        new("Open Cross", "Gapped cross that keeps the target visible.",
            new CrosshairSettings { Shape = CrosshairShape.CrossWithGap, Size = 18, Thickness = 2, Gap = 5 }),
        new("Long Thin", "Wide cross with hairline arms.",
            new CrosshairSettings { Shape = CrosshairShape.Cross, Size = 28, Thickness = 1, Gap = 0 }),
        new("Closed Plus", "Short, solid plus.",
            new CrosshairSettings { Shape = CrosshairShape.Cross, Size = 14, Thickness = 3, Gap = 0 }),
        new("T", "Top bar with a single stem.",
            new CrosshairSettings { Shape = CrosshairShape.TShape, Size = 18, Thickness = 2, Gap = 0 }),
        new("Wide T", "A broad T for busy scenes.",
            new CrosshairSettings { Shape = CrosshairShape.TShape, Size = 30, Thickness = 3, Gap = 0 })
    };
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

    public CrosshairStyleOption? SelectedStyle
    {
        get => _selectedStyle;
        set
        {
            if (!Set(ref _selectedStyle, value) || value is null) return;
            _settings = value.Settings with { Colour = _colour, Opacity = Opacity, Outline = Outline };
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(Thickness));
            OnPropertyChanged(nameof(Gap));
            OnPropertyChanged(nameof(Shape));
            Push();
        }
    }

    public CrosshairSettings PreviewSettings => _settings;

    public override async System.Threading.Tasks.Task LoadAsync()
    {
        var displays = await AppServices.Displays.GetDisplaysAsync(default);
        Displays.Clear();
        foreach (var display in displays) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();

        _settings = _settings with { Shape = Shape, Colour = Colour };
        _selectedStyle = Styles.FirstOrDefault(s => s.Settings.Shape == _settings.Shape)
                         ?? Styles.FirstOrDefault(s => s.Settings.Shape == CrosshairShape.CrossWithGap);
        OnPropertyChanged(nameof(SelectedStyle));
        OnPropertyChanged(nameof(PreviewSettings));
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
        OnPropertyChanged(nameof(PreviewSettings));
        if (Enabled) AppServices.Overlay.Update(_settings);
    }
}
