using System;
using System.Collections.Generic;
using System.Windows.Media;
using Veyra.Core.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Veyra.UI.ViewModels;

/// <summary>A preset plus a small generated preview so cards read as looks, not rows.</summary>
public sealed class PresetCardViewModel
{
    public PresetCardViewModel(Preset preset)
    {
        Preset = preset;
        // Saved looks share a deep blue card; the preset's own values remain the source of truth.
        var (start, end, swatches) = Build(
            "#101C2A", "#7A92A8", "#16293A", "#29465E", "#416A84", "#6E93A8", "#A9C2D0");
        Preview = new LinearGradientBrush(start, end, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        Swatches = swatches;
    }

    public Preset Preset { get; }
    public string Id => Preset.Id;
    public string Name => Preset.Name;
    public string Description => Preset.Description ?? Preset.KindLabel;
    public Brush Preview { get; }
    public IReadOnlyList<Brush> Swatches { get; }

    private static (Color Start, Color End, IReadOnlyList<Brush> Swatches) Build(
        string start, string end, params string[] swatches)
    {
        var colors = new List<Brush>();
        foreach (var swatch in swatches) colors.Add(Frozen(swatch));
        return ((Color)ColorConverter.ConvertFromString(start)!, (Color)ColorConverter.ConvertFromString(end)!, colors);
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
