using Veyra.Core.Models;

namespace Veyra.Storage;

/// <summary>
/// Clamps settings that could otherwise put the window off-screen or produce an
/// unusable configuration. Called on every load and save.
/// </summary>
public static class SettingsValidator
{
    public static AppSettings Normalise(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            WindowWidth = Math.Clamp(double.IsFinite(settings.WindowWidth) ? settings.WindowWidth : 1100, 940, 6000),
            WindowHeight = Math.Clamp(double.IsFinite(settings.WindowHeight) ? settings.WindowHeight : 720, 620, 4000),
            ConfirmationTimeoutSeconds = Math.Clamp(settings.ConfirmationTimeoutSeconds, 5, 60),
            MonitoringIntervalMs = Math.Clamp(settings.MonitoringIntervalMs, 500, 10000),
            Theme = string.IsNullOrWhiteSpace(settings.Theme) ? "Dark" : settings.Theme,
            Accent = string.IsNullOrWhiteSpace(settings.Accent) ? "Violet" : settings.Accent,
            LastPage = string.IsNullOrWhiteSpace(settings.LastPage) ? "Overview" : settings.LastPage,
            SettingsSchemaVersion = "1"
        };
    }
}
