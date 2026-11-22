namespace Veyra.Core.Models;

public enum PresetKind
{
    Colour,
    Display,
    Combined
}

public sealed record Preset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public PresetKind Kind { get; init; } = PresetKind.Colour;
    public ColourSettings Colour { get; init; } = new();
    public DisplayMode? DisplayMode { get; init; }
    public string? MonitorId { get; init; }
    public bool IsBuiltIn { get; init; }
    public bool IsDefault { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string KindLabel => Kind switch
    {
        PresetKind.Colour => "Colour",
        PresetKind.Display => "Display",
        PresetKind.Combined => "Combined",
        _ => Kind.ToString()
    };
}

public sealed record GameProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ExecutableName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? PresetId { get; init; }
    public int Priority { get; init; } = 100;
    public bool AutoSwitch { get; init; } = true;
    /// <summary>Display-mode changes are opt-in per game and never happen silently.</summary>
    public bool AllowDisplayChanges { get; init; }
    public DateTimeOffset? LastDetected { get; init; }
    public string? Notes { get; init; }
}

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public enum HotkeyAction
{
    ToggleColourEnhancements,
    NextPreset,
    RestoreDefaults,
    ToggleCrosshair,
    OpenVeyra,
    ActivateProfile
}

public sealed record HotkeySpec(HotkeyModifiers Modifiers, int VirtualKey)
{
    public bool IsEmpty => VirtualKey == 0;

    public static HotkeySpec None { get; } = new(HotkeyModifiers.None, 0);
}

public sealed record HotkeyBinding
{
    public required HotkeyAction Action { get; init; }
    public HotkeySpec Spec { get; init; } = HotkeySpec.None;
    public string? PresetId { get; init; }
    public bool Enabled { get; init; } = true;

    public string ActionLabel => Action switch
    {
        HotkeyAction.ToggleColourEnhancements => "Toggle colour enhancements",
        HotkeyAction.NextPreset => "Switch to next preset",
        HotkeyAction.RestoreDefaults => "Restore desktop settings",
        HotkeyAction.ToggleCrosshair => "Toggle crosshair",
        HotkeyAction.OpenVeyra => "Open Veyra",
        HotkeyAction.ActivateProfile => "Activate a specific preset",
        _ => Action.ToString()
    };
}
