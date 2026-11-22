using System.Runtime.InteropServices;
using Veyra.Display.Interop;

namespace Veyra.Display.Monitors;

internal sealed record CcdTarget(
    string GdiName,
    string FriendlyName,
    string MonitorPath,
    bool HdrSupported,
    bool HdrEnabled,
    CcdNative.Luid AdapterId,
    uint TargetId);

/// <summary>
/// Reads active CCD paths to learn target identity, friendly names and HDR state.
/// Read-only: no topology changes are performed here.
/// </summary>
internal static class CcdReader
{
    public static IReadOnlyDictionary<string, CcdTarget> ReadActiveTargets()
    {
        var result = new Dictionary<string, CcdTarget>(StringComparer.OrdinalIgnoreCase);

        var status = CcdNative.GetDisplayConfigBufferSizes(CcdNative.QdcOnlyActivePaths, out var pathCount, out var modeCount);
        if (status != 0 || pathCount == 0) return result;

        var paths = new CcdNative.PathInfo[pathCount];
        var modes = new CcdNative.ModeInfo[Math.Max(modeCount, 1)];

        status = CcdNative.QueryDisplayConfig(
            CcdNative.QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (status != 0) return result;

        for (var i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var gdiName = ReadSourceName(path);
            if (string.IsNullOrEmpty(gdiName)) continue;

            var target = ReadTargetName(path);
            var hdr = ReadHdr(path);

            result[gdiName] = new CcdTarget(
                gdiName,
                target?.MonitorFriendlyDeviceName?.Trim() ?? string.Empty,
                target?.MonitorDevicePath?.Trim() ?? string.Empty,
                hdr?.Supported ?? false,
                hdr?.Enabled ?? false,
                path.TargetInfo.AdapterId,
                path.TargetInfo.Id);
        }

        return result;
    }

    private static string? ReadSourceName(CcdNative.PathInfo path)
    {
        var source = new CcdNative.SourceDeviceName
        {
            Header = new CcdNative.DeviceInfoHeader
            {
                Type = CcdNative.GetSourceName,
                Size = (uint)Marshal.SizeOf<CcdNative.SourceDeviceName>(),
                AdapterId = path.SourceInfo.AdapterId,
                Id = path.SourceInfo.Id
            },
            ViewGdiDeviceName = string.Empty
        };

        return CcdNative.DisplayConfigGetDeviceInfo(ref source) == 0 ? source.ViewGdiDeviceName : null;
    }

    private static CcdNative.TargetDeviceName? ReadTargetName(CcdNative.PathInfo path)
    {
        var target = new CcdNative.TargetDeviceName
        {
            Header = new CcdNative.DeviceInfoHeader
            {
                Type = CcdNative.GetTargetName,
                Size = (uint)Marshal.SizeOf<CcdNative.TargetDeviceName>(),
                AdapterId = path.TargetInfo.AdapterId,
                Id = path.TargetInfo.Id
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        return CcdNative.DisplayConfigGetDeviceInfo(ref target) == 0 ? target : null;
    }

    private static (bool Supported, bool Enabled)? ReadHdr(CcdNative.PathInfo path)
    {
        var info = new CcdNative.GetAdvancedColorInfo
        {
            Header = new CcdNative.DeviceInfoHeader
            {
                Type = CcdNative.GetAdvancedColorInfoType,
                Size = (uint)Marshal.SizeOf<CcdNative.GetAdvancedColorInfo>(),
                AdapterId = path.TargetInfo.AdapterId,
                Id = path.TargetInfo.Id
            }
        };

        if (CcdNative.DisplayConfigGetDeviceInfo(ref info) != 0) return null;

        return ((info.Value & CcdNative.AdvancedColorSupported) != 0,
                (info.Value & CcdNative.AdvancedColorEnabled) != 0);
    }

    public static CcdTarget? FindByGdiName(string gdiDeviceName) =>
        ReadActiveTargets().TryGetValue(gdiDeviceName, out var target) ? target : null;
}
