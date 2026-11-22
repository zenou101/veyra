namespace Veyra.Core.Models;

public enum DisplayOrientation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3
}

public sealed record DisplayMode(int Width, int Height, int RefreshRate, int BitsPerPixel, int Orientation)
{
    public string Label => $"{Width} × {Height} @ {RefreshRate} Hz";
    public string Compact => $"{Width}×{Height}@{RefreshRate}";
    public double AspectRatio => Height == 0 ? 0 : Math.Round((double)Width / Height, 3);
}

public sealed record DisplayInfo
{
    public required string Id { get; init; }
    public required string GdiDeviceName { get; init; }
    public required string FriendlyName { get; init; }
    public string? Manufacturer { get; init; }
    public string? MonitorDevicePath { get; init; }
    public required string AdapterName { get; init; }
    public string? VendorId { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int RefreshRate { get; init; }
    public int BitsPerPixel { get; init; }
    public DisplayOrientation Orientation { get; init; }
    public bool IsPrimary { get; init; }
    public bool HdrSupported { get; init; }
    public bool HdrEnabled { get; init; }
    public bool Connected { get; init; } = true;
    public string? ColourProfile { get; init; }

    public string ResolutionLabel => $"{Width} × {Height}";
    public string RefreshLabel => RefreshRate > 0 ? $"{RefreshRate} Hz" : "Unknown";
    public string HdrLabel => !HdrSupported ? "Not supported" : HdrEnabled ? "On" : "Available";
}

/// <summary>Exact per-display state captured before a change, used by the recovery guard.</summary>
public sealed record DisplayState(
    string Id,
    string GdiDeviceName,
    int Width,
    int Height,
    int RefreshRate,
    int BitsPerPixel,
    int Orientation,
    int PositionX,
    int PositionY,
    int DisplayFixedOutput,
    int DisplayFlags);

public sealed record DisplayChangeRequest(string DisplayId, DisplayMode TargetMode);
