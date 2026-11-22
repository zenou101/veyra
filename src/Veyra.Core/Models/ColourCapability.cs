namespace Veyra.Core.Models;

public enum ColourControl
{
    Brightness,
    Contrast,
    Gamma,
    Saturation,
    Hue,
    Temperature,
    Red,
    Green,
    Blue,
    Shadow,
    BlackLevel
}

/// <summary>
/// One adjustable colour parameter with the range the driver actually reports.
/// Anything unsupported is surfaced as such, never as a working slider.
/// </summary>
public sealed record ColourControlCapability(
    ColourControl Control,
    bool IsSupported,
    double Minimum,
    double Maximum,
    double Step,
    double Default,
    string Unit,
    string Source,
    string Reason)
{
    public static ColourControlCapability Supported(ColourControl control, double min, double max, double step,
        double def, string unit, string source) =>
        new(control, true, min, max, step <= 0 ? 1 : step, def, unit, source, string.Empty);

    public static ColourControlCapability Unsupported(ColourControl control, string reason, string source = "none") =>
        new(control, false, 0, 0, 0, 0, string.Empty, source, reason);

    public string DisplayName => Control switch
    {
        ColourControl.Brightness => "Brightness",
        ColourControl.Contrast => "Contrast",
        ColourControl.Gamma => "Gamma",
        ColourControl.Saturation => "Saturation",
        ColourControl.Hue => "Hue",
        ColourControl.Temperature => "Colour temperature",
        ColourControl.Red => "Red channel",
        ColourControl.Green => "Green channel",
        ColourControl.Blue => "Blue channel",
        ColourControl.Shadow => "Shadow brightness",
        ColourControl.BlackLevel => "Black level",
        _ => Control.ToString()
    };
}

public sealed record ColourCapabilities(
    string ProviderName,
    string Vendor,
    string DisplayId,
    IReadOnlyList<ColourControlCapability> Controls,
    string? Summary = null)
{
    public ColourControlCapability? Get(ColourControl control) => Controls.FirstOrDefault(c => c.Control == control);

    public bool AnySupported => Controls.Any(c => c.IsSupported);

    public static ColourCapabilities None(string vendor, string displayId, string reason) => new(
        "none", vendor, displayId,
        Enum.GetValues<ColourControl>().Select(c => ColourControlCapability.Unsupported(c, reason)).ToArray(),
        reason);
}

/// <summary>State the driver currently reports for a display.</summary>
public sealed record ColourState(string DisplayId, ColourSettings Values, string ProviderName);

public sealed record ColourProviderInfo(string Name, string Vendor, bool Available, string Detail);
