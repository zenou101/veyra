using System;
using System.Windows;
using System.Windows.Media;
using Veyra.Core.Models;
using Color = System.Windows.Media.Color;

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

        if (Application.Current.Resources["CardGradientBrush"] is LinearGradientBrush card)
        {
            if (!card.IsFrozen)
            {
                for (var i = 0; i < card.GradientStops.Count && i < stops.Length; i++)
                {
                    var stop = card.GradientStops[i];
                    stop.Color = Color.FromArgb((byte)Math.Round(opacity * 255), stops[i].R, stops[i].G, stops[i].B);
                }
            }
        }

        if (Application.Current.Resources["PanelBrush"] is SolidColorBrush panel && !panel.IsFrozen)
        {
            panel.Color = Color.FromArgb((byte)Math.Round(opacity * 255), stops[1].R, stops[1].G, stops[1].B);
        }

        Changed?.Invoke();
    }

    private static double SettingsOpacity(AppSettings settings) =>
        double.IsFinite(settings.PanelOpacity) ? Math.Clamp(settings.PanelOpacity, 0.55, 1.0) : 0.92;
}
