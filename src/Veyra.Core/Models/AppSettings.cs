namespace Veyra.Core.Models;

public sealed record AppSettings
{
    public double WindowWidth { get; init; } = 1100;
    public double WindowHeight { get; init; } = 720;
    public double WindowLeft { get; init; } = double.NaN;
    public double WindowTop { get; init; } = double.NaN;
    public bool WindowMaximized { get; init; }

    public bool LaunchWithWindows { get; init; }
    public bool StartMinimized { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public string Theme { get; init; } = "Dark";
    public string Accent { get; init; } = "Violet";
    public string Language { get; init; } = "en-GB";

    public bool RestoreOnExit { get; init; } = true;
    public bool AutomaticGameProfiles { get; init; } = true;
    public bool ProfileNotifications { get; init; } = true;
    public int ConfirmationTimeoutSeconds { get; init; } = 15;
    public string MultiMonitorBehaviour { get; init; } = "SelectedDisplay";

    public int MonitoringIntervalMs { get; init; } = 1000;
    public bool MonitoringEnabled { get; init; } = true;
    public bool PerformanceOverlayEnabled { get; init; }
    public bool CrosshairEnabled { get; init; }
    public bool CrosshairAllMonitors { get; init; }
    public string? CrosshairDisplayId { get; init; }

    public bool CheckForUpdates { get; init; }
    public string LastPage { get; init; } = "Overview";
    public bool FirstRunCompleted { get; init; }
    public string SettingsSchemaVersion { get; init; } = "1";
}
