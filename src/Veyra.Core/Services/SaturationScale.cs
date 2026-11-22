namespace Veyra.Core.Services;

/// <summary>
/// Maps Veyra's saturation percentage onto the native range a GPU driver reports.
/// 100 % is the driver's neutral value, 200 % is its advertised maximum, and 300 % is
/// the verified AMD extended ceiling. NVIDIA Digital Vibrance is bounded by the maximum
/// NVAPI reports. Saturation is a real driver colour control, not a gamma curve.
/// </summary>
public static class SaturationScale
{
    public const double MinimumPercent = 0;
    public const double NeutralPercent = 100;

    /// <summary>Highest percentage Veyra exposes on the verified AMD extended range.</summary>
    public const double MaximumPercent = 300;

    public static double ClampPercent(double percent, double maximumPercent = MaximumPercent)
    {
        if (!double.IsFinite(percent)) return NeutralPercent;
        var maximum = NormaliseMaximum(maximumPercent);
        return Math.Clamp(percent, MinimumPercent, maximum);
    }

    public static int ToNative(double percent, int minimum, int neutral, int maximum, int step,
        double maximumPercent = MaximumPercent)
    {
        var value = ClampPercent(percent, maximumPercent);
        var native = value <= NeutralPercent
            ? minimum + (value / NeutralPercent) * (neutral - minimum)
            : neutral + ((value - NeutralPercent) / NeutralPercent) * (maximum - neutral);

        var increment = Math.Max(step, 1);
        var rounded = (int)Math.Round(native / increment, MidpointRounding.AwayFromZero) * increment;

        // Below neutral the native minimum is the hard floor. Above it, the caller's
        // maximumPercent is the contract: 300 % on the verified AMD driver maps to native
        // 300, while a driver capped at 200 % maps back to its advertised native maximum.
        var upperPercent = NormaliseMaximum(maximumPercent);
        var upperNative = neutral + ((upperPercent - NeutralPercent) / NeutralPercent) * (maximum - neutral);
        var lowerNative = minimum;
        var lower = (int)Math.Min(lowerNative, upperNative);
        var upper = (int)Math.Max(lowerNative, upperNative);
        return Math.Clamp(rounded, lower, upper);
    }

    public static double ToPercent(int value, int minimum, int neutral, int maximum,
        double maximumPercent = MaximumPercent)
    {
        if (value <= neutral && neutral != minimum)
        {
            return ClampPercent(NeutralPercent * (value - minimum) / (neutral - minimum), maximumPercent);
        }

        if (value > neutral && maximum != neutral)
        {
            return ClampPercent(
                NeutralPercent + NeutralPercent * (value - neutral) / (maximum - neutral),
                maximumPercent);
        }

        return NeutralPercent;
    }

    /// <summary>
    /// The UI maximum for a driver that only accepts its advertised range. A normal DVC
    /// range (for example 0..100 with 50 neutral) maps to 200 %; a non-boosting range maps
    /// to neutral only.
    /// </summary>
    public static double PercentAtNativeMaximum(int neutral, int maximum) =>
        maximum > neutral ? NeutralPercent * 2 : NeutralPercent;

    private static double NormaliseMaximum(double maximumPercent) =>
        double.IsFinite(maximumPercent)
            ? Math.Clamp(maximumPercent, NeutralPercent, MaximumPercent)
            : MaximumPercent;
}
