using System;
using System.Windows;
using System.Windows.Media;
using Veyra.Core.Models;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Veyra.UI.Infrastructure;

/// <summary>
/// Applies the Customize page's panel transparency to the shared card gradient. Text stays
/// fully opaque: only the panel background's alpha changes, matching the original glass cards.
/// </summary>
public static class AppearanceService
{
    public static event Action? Changed;

    public static void Apply()
    {
        if (Application.Current is null) return;

        var settings = AppServices.Settings;
        var slate = string.Equals(settings.AppearanceMode, "Slate", StringComparison.OrdinalIgnoreCase);
        var opacity = SettingsOpacity(settings) * (slate ? 0.82 : 1.0);

        var stops = slate
            ? new[] { Color.FromRgb(10, 28, 37), Color.FromRgb(7, 19, 26), Color.FromRgb(5, 13, 18) }
            : new[] { Color.FromRgb(12, 28, 37), Color.FromRgb(8, 20, 27), Color.FromRgb(6, 16, 22) };

        // Theme brushes are frozen by WPF, so mutating the shared instance silently does
        // nothing. Replace the resource instead; every Card uses DynamicResource and updates.
        var card = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        var alpha = (byte)Math.Round(opacity * 255);
        for (var i = 0; i < stops.Length; i++)
        {
            card.GradientStops.Add(new GradientStop(
                Color.FromArgb(alpha, stops[i].R, stops[i].G, stops[i].B), i / (double)(stops.Length - 1)));
        }
        card.Freeze();
        Application.Current.Resources["CardGradientBrush"] = card;

        Changed?.Invoke();
    }

    private static double SettingsOpacity(AppSettings settings) =>
        double.IsFinite(settings.PanelOpacity) ? Math.Clamp(settings.PanelOpacity, 0.55, 1.0) : 0.92;
}
