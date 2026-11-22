using System.Runtime.InteropServices;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Display.Interop;

namespace Veyra.Display.Monitors;

/// <summary>
/// Enumerates connected displays with their real modes and HDR state and applies
/// mode changes through ChangeDisplaySettingsEx. Every failure is reported.
/// </summary>
public sealed partial class DisplayService : IDisplayService
{
    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken) =>
        Task.Run(EnumerateDisplays, cancellationToken);

    public static IReadOnlyList<DisplayInfo> EnumerateDisplays()
    {
        var targets = CcdReader.ReadActiveTargets();
        var displays = new List<DisplayInfo>();

        uint index = 0;
        while (true)
        {
            var adapter = default(DisplayNative.DisplayDevice);
            adapter.Size = (uint)Marshal.SizeOf<DisplayNative.DisplayDevice>();
            if (!DisplayNative.EnumDisplayDevices(null, index, ref adapter, 0)) break;
            index++;

            if ((adapter.StateFlags & DisplayNative.DisplayDeviceAttachedToDesktop) == 0) continue;

            var monitor = default(DisplayNative.DisplayDevice);
            monitor.Size = (uint)Marshal.SizeOf<DisplayNative.DisplayDevice>();
            DisplayNative.EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, 0);

            var devMode = DisplayNative.CreateDevMode();
            if (!DisplayNative.EnumDisplaySettingsEx(adapter.DeviceName, DisplayNative.EnumCurrentSettings, ref devMode, 0))
            {
                continue;
            }

            targets.TryGetValue(adapter.DeviceName, out var ccd);

            displays.Add(new DisplayInfo
            {
                Id = string.IsNullOrWhiteSpace(ccd?.MonitorPath) ? adapter.DeviceName : ccd!.MonitorPath,
                GdiDeviceName = adapter.DeviceName,
                FriendlyName = FirstNonEmpty(ccd?.FriendlyName, monitor.DeviceString, adapter.DeviceString, adapter.DeviceName),
                Manufacturer = BuildManufacturer(monitor.DeviceId, ccd?.MonitorPath),
                MonitorDevicePath = NullIfEmpty(string.IsNullOrWhiteSpace(ccd?.MonitorPath) ? monitor.DeviceId : ccd!.MonitorPath),
                AdapterName = FirstNonEmpty(adapter.DeviceString, "Graphics adapter"),
                VendorId = DescribeVendor(adapter.DeviceString),
                Width = (int)devMode.PelsWidth,
                Height = (int)devMode.PelsHeight,
                RefreshRate = (int)devMode.DisplayFrequency,
                BitsPerPixel = (int)devMode.BitsPerPel,
                Orientation = (DisplayOrientation)devMode.DisplayOrientation,
                IsPrimary = (adapter.StateFlags & DisplayNative.DisplayDevicePrimaryDevice) != 0 ||
                            (devMode.PositionX == 0 && devMode.PositionY == 0),
                HdrSupported = ccd?.HdrSupported ?? false,
                HdrEnabled = ccd?.HdrEnabled ?? false,
                Connected = true,
                ColourProfile = ColourProfileReader.GetProfileName(adapter.DeviceName)
            });
        }

        return displays
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? DescribeVendor(string adapterName)
    {
        if (adapterName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            adapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) return "AMD";
        if (adapterName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            adapterName.Contains("GeForce", StringComparison.OrdinalIgnoreCase)) return "NVIDIA";
        if (adapterName.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return "Intel";
        return null;
    }

    private static string? BuildManufacturer(string deviceId, string? monitorPath)
    {
        var source = !string.IsNullOrWhiteSpace(deviceId) ? deviceId : monitorPath;
        if (string.IsNullOrWhiteSpace(source)) return null;

        var marker = source.IndexOf("MONITOR", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;
        var rest = source[(marker + 8)..];
        var end = rest.IndexOf((char)92);
        var code = end > 0 ? rest[..end] : rest;
        return code.Length >= 3 ? code[..3] : code;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))!.Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static bool Matches(DisplayInfo display, string id) =>
        string.Equals(display.Id, id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(display.GdiDeviceName, id, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(display.MonitorDevicePath, id, StringComparison.OrdinalIgnoreCase);
}
