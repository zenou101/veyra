namespace Veyra.Core.Models;

public enum CrosshairShape
{
    Dot,
    Cross,
    Circle,
    CrossWithGap,
    TShape
}

public sealed record CrosshairSettings
{
    public CrosshairShape Shape { get; init; } = CrosshairShape.CrossWithGap;
    public double Size { get; init; } = 18;
    public double Thickness { get; init; } = 2;
    public double Gap { get; init; } = 4;
    public string Colour { get; init; } = "#8B7CF6";
    public double Opacity { get; init; } = 0.9;
    public bool Outline { get; init; } = true;
    public double OutlineThickness { get; init; } = 1;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }

    public static CrosshairSettings Default { get; } = new();

    /// <summary>Rendering warnings shown in the UI; the overlay is a plain topmost layered window.</summary>
    public static IReadOnlyList<string> CompatibilityNotes { get; } = new[]
    {
        "Works on the desktop and in windowed or borderless games.",
        "Exclusive-fullscreen games may draw over the overlay; Veyra cannot guarantee visibility there.",
        "The overlay never injects code, hooks rendering or interacts with anti-cheat systems.",
        "Some competitive games restrict overlays by policy. Check the game's rules before enabling."
    };
}
