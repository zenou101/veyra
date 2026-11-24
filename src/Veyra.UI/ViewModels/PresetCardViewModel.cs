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
        var (start, end, swatches) = PaletteFor(preset);
        Preview = new LinearGradientBrush(start, end, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        Swatches = swatches;
    }

    public Preset Preset { get; }
    public string Id => Preset.Id;
    public string Name => Preset.Name;
    public string Description => Preset.Description ?? Preset.KindLabel;
    public bool IsBuiltIn => Preset.IsBuiltIn;
    public bool CanDelete => !Preset.IsBuiltIn;
    public string Badge => Preset.IsBuiltIn ? "BUILT-IN" : "SAVED";
    public Brush Preview { get; }
    public IReadOnlyList<Brush> Swatches { get; }

    private static (Color Start, Color End, IReadOnlyList<Brush> Swatches) PaletteFor(Preset preset)
    {
        var key = preset.Id.ToLowerInvariant();
        if (key.Contains("night")) return Build("#080D1A", "#2E3C63", "#0B1224", "#1A2744", "#33456E", "#5E6E94", "#98A4BF");
        if (key.Contains("cool")) return Build("#0F2440", "#5B8FD6", "#17385E", "#2B5C93", "#5B8FD6", "#8FB6E6", "#C6DDF4");
        if (key.Contains("warm")) return Build("#2A1810", "#C98A4E", "#5A3320", "#8A4E2B", "#B96F3F", "#D99A62", "#E8C39A");
        if (key.Contains("competitive")) return Build("#241310", "#A8573A", "#4A2118", "#7D3324", "#A8573A", "#C47B52", "#D9A77E");
        if (key.Contains("vibrant")) return Build("#1A0F3A", "#1E8FA8", "#3A1F6E", "#6B3FB0", "#2A7FB8", "#1E9FA8", "#46C6C2");
        if (key.Contains("balanced")) return Build("#0A1F24", "#3E7D77", "#0C2A32", "#15504F", "#2A736C", "#4C9A8B", "#7FBBA8");
        if (key.Contains("default")) return Build("#0A151C", "#35505C", "#0B1D26", "#183642", "#2E5663", "#507A85", "#8FA6AB");

        // A user-saved look gets a deep blue card; the preset's own values remain the source of truth.
        return Build("#101C2A", "#7A92A8", "#16293A", "#29465E", "#416A84", "#6E93A8", "#A9C2D0");
    }

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
