using Veyra.Core.Models;

namespace Veyra.Core.Services;

/// <summary>
/// Builds the 3 × 256 entry gamma ramp that the Windows GDI layer accepts.
/// Saturation is deliberately not handled here: a per-channel ramp cannot mix channels, so it
/// cannot produce true saturation. Saturation is applied by the AMD or NVIDIA driver's own colour control instead.
/// </summary>
public static class ColourRampCalculator
{
    public const int RampSize = 256;

    public static ushort[] BuildRamp(ColourSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var gamma = Math.Clamp(settings.Gamma ?? 1.0, 0.25, 4.0);
        var brightness = Math.Clamp((settings.Brightness ?? 0) / 100.0, -0.5, 0.5);
        var contrast = Math.Clamp((settings.Contrast ?? 0) / 100.0, -0.9, 1.0);
        var shadow = Math.Clamp((settings.Shadow ?? 0) / 100.0, -1.0, 1.0);
        var black = Math.Clamp((settings.BlackLevel ?? 0) / 100.0, 0.0, 0.5);
        var redGain = Math.Clamp((settings.Red ?? 0) / 100.0, -1.0, 1.0);
        var greenGain = Math.Clamp((settings.Green ?? 0) / 100.0, -1.0, 1.0);
        var blueGain = Math.Clamp((settings.Blue ?? 0) / 100.0, -1.0, 1.0);

        var (tempRed, tempGreen, tempBlue) = TemperatureGains(settings.Temperature ?? 6500);

        var ramp = new ushort[RampSize * 3];
        for (var i = 0; i < RampSize; i++)
        {
            var x = i / (double)(RampSize - 1);

            if (black > 0)
            {
                var floor = black * 0.5;
                x = x <= floor ? 0 : (x - floor) / (1 - floor);
            }

            x = Math.Pow(x, gamma);
            x = (x - 0.5) * (1 + contrast) + 0.5;

            if (shadow != 0)
            {
                x += shadow * 0.35 * Math.Pow(Math.Clamp(1 - x, 0, 1), 3);
            }

            x += brightness * 0.5;

            ramp[i] = ToRampValue(x * (1 + redGain) * tempRed);
            ramp[RampSize + i] = ToRampValue(x * (1 + greenGain) * tempGreen);
            ramp[RampSize * 2 + i] = ToRampValue(x * (1 + blueGain) * tempBlue);
        }

        return ramp;
    }

    public static (double Red, double Green, double Blue) TemperatureGains(double kelvin)
    {
        var t = Math.Clamp((Math.Clamp(kelvin, 2000, 12000) - 6500) / 4500.0, -1.0, 1.0);
        return (1 - 0.15 * t, 1.0, 1 + 0.15 * t);
    }

    private static ushort ToRampValue(double value) =>
        (ushort)Math.Round(Math.Clamp(value, 0.0, 1.0) * ushort.MaxValue);

    /// <summary>True when the settings describe a neutral ramp, used to decide whether a restore is needed.</summary>
    public static bool IsNeutral(ColourSettings settings) =>
        (settings.Brightness ?? 0) == 0 &&
        (settings.Contrast ?? 0) == 0 &&
        (settings.Gamma ?? 1) == 1 &&
        (settings.Red ?? 0) == 0 &&
        (settings.Green ?? 0) == 0 &&
        (settings.Blue ?? 0) == 0 &&
        (settings.Shadow ?? 0) == 0 &&
        (settings.BlackLevel ?? 0) == 0 &&
        (settings.Temperature ?? 6500) == 6500;

    public static string Describe(ColourSettings settings)
    {
        var parts = new List<string>();
        if (settings.Brightness is { } b && b != 0) parts.Add($"brightness {b:+0;-0}");
        if (settings.Contrast is { } c && c != 0) parts.Add($"contrast {c:+0;-0}");
        if (settings.Gamma is { } g && Math.Abs(g - 1) > 0.001) parts.Add($"gamma {g:0.00}");
        if (settings.Temperature is { } t && Math.Abs(t - 6500) > 1) parts.Add($"{t:0} K");
        if (settings.Shadow is { } sh && sh != 0) parts.Add($"shadows {sh:+0;-0}");
        if (settings.BlackLevel is { } bl && bl != 0) parts.Add($"black level {bl:0}");
        return parts.Count == 0 ? "Neutral" : string.Join(", ", parts);
    }
}
