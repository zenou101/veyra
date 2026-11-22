using System.Runtime.InteropServices;
using Veyra.Display.Interop;
using Xunit;

namespace Veyra.Display.Tests;

/// <summary>
/// QueryDisplayConfig writes native-sized records into these arrays. A managed struct that is
/// smaller than the SDK struct makes Windows overwrite the managed heap (0xC0000374).
/// Sizes were taken from the Windows 10 SDK 10.0.26100 headers (sizeof in C).
/// </summary>
public sealed class CcdStructLayoutTests
{
    [Fact]
    public void PathTargetInfo_MatchesSdk() => Assert.Equal(48, Marshal.SizeOf<CcdNative.PathTargetInfo>());

    [Fact]
    public void PathInfo_MatchesSdk() => Assert.Equal(72, Marshal.SizeOf<CcdNative.PathInfo>());

    [Fact]
    public void ModeInfo_MatchesSdk() => Assert.Equal(64, Marshal.SizeOf<CcdNative.ModeInfo>());

    [Fact]
    public void DeviceInfoHeader_MatchesSdk() => Assert.Equal(20, Marshal.SizeOf<CcdNative.DeviceInfoHeader>());

    [Fact]
    public void SourceDeviceName_MatchesSdk() => Assert.Equal(84, Marshal.SizeOf<CcdNative.SourceDeviceName>());

    [Fact]
    public void TargetDeviceName_MatchesSdk() => Assert.Equal(420, Marshal.SizeOf<CcdNative.TargetDeviceName>());

    [Fact]
    public void AdvancedColorInfo_MatchesSdk() => Assert.Equal(32, Marshal.SizeOf<CcdNative.GetAdvancedColorInfo>());
}
