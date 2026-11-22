namespace Veyra.Core.Models;

/// <summary>Values requested by the user. Null means "not part of this preset".</summary>
public sealed record ColourSettings
{
    public double? Brightness { get; init; }
    public double? Contrast { get; init; }
    public double? Gamma { get; init; }
    public double? Saturation { get; init; }
    public double? Hue { get; init; }
    public double? Temperature { get; init; }
    public double? Red { get; init; }
    public double? Green { get; init; }
    public double? Blue { get; init; }
    public double? Shadow { get; init; }
    public double? BlackLevel { get; init; }

    /// <summary>Saturation is a percentage: 100 is neutral, 0 greyscale, 200 reaches the advertised driver maximum, and the verified AMD extended ceiling is 300.</summary>
    public static ColourSettings Neutral { get; } = new()
    {
        Brightness = 0,
        Contrast = 0,
        Gamma = 1,
        Saturation = 100,
        Temperature = 6500,
        Red = 0,
        Green = 0,
        Blue = 0,
        Shadow = 0,
        BlackLevel = 0
    };

    public bool IsEmpty =>
        Brightness is null && Contrast is null && Gamma is null && Saturation is null && Hue is null &&
        Temperature is null && Red is null && Green is null && Blue is null && Shadow is null && BlackLevel is null;

    public double? Get(ColourControl control) => control switch
    {
        ColourControl.Brightness => Brightness,
        ColourControl.Contrast => Contrast,
        ColourControl.Gamma => Gamma,
        ColourControl.Saturation => Saturation,
        ColourControl.Hue => Hue,
        ColourControl.Temperature => Temperature,
        ColourControl.Red => Red,
        ColourControl.Green => Green,
        ColourControl.Blue => Blue,
        ColourControl.Shadow => Shadow,
        ColourControl.BlackLevel => BlackLevel,
        _ => null
    };

    public ColourSettings With(ColourControl control, double? value) => control switch
    {
        ColourControl.Brightness => this with { Brightness = value },
        ColourControl.Contrast => this with { Contrast = value },
        ColourControl.Gamma => this with { Gamma = value },
        ColourControl.Saturation => this with { Saturation = value },
        ColourControl.Hue => this with { Hue = value },
        ColourControl.Temperature => this with { Temperature = value },
        ColourControl.Red => this with { Red = value },
        ColourControl.Green => this with { Green = value },
        ColourControl.Blue => this with { Blue = value },
        ColourControl.Shadow => this with { Shadow = value },
        ColourControl.BlackLevel => this with { BlackLevel = value },
        _ => this
    };
}
