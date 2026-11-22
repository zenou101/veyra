using Veyra.Core.Services;
using Xunit;

namespace Veyra.Core.Tests;

public sealed class SaturationScaleTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(150, 150)]
    [InlineData(200, 200)]
    [InlineData(250, 250)]
    [InlineData(300, 300)]
    public void NativeRangeMatchesPercentWhenDriverIsLinear(double percent, int expected) =>
        Assert.Equal(expected, SaturationScale.ToNative(percent, 0, 100, 200, 1));

    [Fact]
    public void UsesTheDriverReportedNeutralPoint()
    {
        Assert.Equal(120, SaturationScale.ToNative(100, 0, 120, 220, 1));
        Assert.Equal(220, SaturationScale.ToNative(200, 0, 120, 220, 1));
        Assert.Equal(320, SaturationScale.ToNative(300, 0, 120, 220, 1));
        Assert.Equal(60, SaturationScale.ToNative(50, 0, 120, 220, 1));
    }

    [Fact]
    public void RoundsToTheDriverStep()
    {
        Assert.Equal(150, SaturationScale.ToNative(150, 0, 100, 200, 5));
        Assert.Equal(152, SaturationScale.ToNative(151, 0, 100, 200, 4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(150, 150)]
    [InlineData(200, 200)]
    [InlineData(300, 300)]
    public void PercentRoundTrips(int native, double expected) =>
        Assert.Equal(expected, SaturationScale.ToPercent(native, 0, 100, 200));

    [Fact]
    public void NvidiaDriverMaximumCapsTheRequestedPercent()
    {
        Assert.Equal(100, SaturationScale.ToNative(300, 0, 50, 100, 1, 200));
        Assert.Equal(50, SaturationScale.ToNative(100, 0, 50, 100, 1, 200));
        Assert.Equal(200, SaturationScale.ToPercent(100, 0, 50, 100, 200));
        Assert.Equal(200, SaturationScale.PercentAtNativeMaximum(50, 100));
        Assert.Equal(100, SaturationScale.PercentAtNativeMaximum(50, 50));
    }

    [Fact]
    public void ColourProfileImportAcceptsTheVerifiedExtendedCeiling()
    {
        const string json = "{\"schema\":\"veyra.profile/1\",\"name\":\"Vivid\",\"kind\":\"Colour\",\"colour\":{\"Saturation\":300}}";
        var result = ProfileJson.Import(json, out var preset);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(preset);
        Assert.Equal(300, preset!.Colour.Saturation);
    }

    [Fact]
    public void ColourProfileImportRejectsValuesAboveTheCeiling()
    {
        const string json = "{\"schema\":\"veyra.profile/1\",\"name\":\"Too much\",\"kind\":\"Colour\",\"colour\":{\"Saturation\":301}}";
        var result = ProfileJson.Import(json, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("Saturation", result.Message);
    }

    [Fact]
    public void ClampsUnsafeInput()
    {
        Assert.Equal(300, SaturationScale.ToNative(500, 0, 100, 200, 1));
        Assert.Equal(0, SaturationScale.ToNative(-20, 0, 100, 200, 1));
        Assert.Equal(100, SaturationScale.ClampPercent(double.NaN));
        Assert.Equal(100, SaturationScale.ClampPercent(double.PositiveInfinity));
    }
}
