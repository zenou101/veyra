using System;
using System.Runtime.InteropServices;
using Veyra.Core.Models;

namespace Veyra.UI.Services;

/// <summary>
/// Minimal DDC/CI bridge through dxva2. Every monitor supports a different subset of
/// controls, so nothing is assumed: a control is only exposed when Windows reads a real
/// range for it. Handles are opened per operation and always destroyed.
/// </summary>
public sealed class MonitorDdc : IDisposable
{
    private IntPtr _handle;

    private MonitorDdc(IntPtr handle, string description)
    {
        _handle = handle;
        Description = description;
    }

    public string Description { get; private set; } = "Monitor";
    public bool HasBrightness { get; private set; }
    public bool HasContrast { get; private set; }
    public uint BrightnessMinimum { get; private set; }
    public uint BrightnessMaximum { get; private set; }
    public uint BrightnessCurrent { get; private set; }
    public uint ContrastMinimum { get; private set; }
    public uint ContrastMaximum { get; private set; }
    public uint ContrastCurrent { get; private set; }

    public static MonitorDdc? TryOpen(DisplayInfo display)
    {
        try
        {
            var monitor = FindMonitor(display.GdiDeviceName);
            if (monitor == IntPtr.Zero) return null;

            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0) return null;

            var physical = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical) || physical.Length == 0) return null;

            // Use the first physical monitor; a display path normally owns exactly one.
            var ddc = new MonitorDdc(physical[0].Handle,
                string.IsNullOrWhiteSpace(physical[0].Description) ? display.FriendlyName : physical[0].Description);

            for (var i = 1; i < physical.Length; i++)
            {
                DestroyPhysicalMonitors(1, new[] { physical[i] });
            }

            if (GetMonitorBrightness(ddc._handle, out var bMin, out var bCurrent, out var bMax) && bMax > bMin)
            {
                ddc.HasBrightness = true;
                ddc.BrightnessMinimum = bMin;
                ddc.BrightnessMaximum = bMax;
                ddc.BrightnessCurrent = bCurrent;
            }

            if (GetMonitorContrast(ddc._handle, out var cMin, out var cCurrent, out var cMax) && cMax > cMin)
            {
                ddc.HasContrast = true;
                ddc.ContrastMinimum = cMin;
                ddc.ContrastMaximum = cMax;
                ddc.ContrastCurrent = cCurrent;
            }

            return ddc;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool SetBrightnessPercent(double percent)
    {
        if (!HasBrightness) return false;
        var value = (uint)Math.Round(BrightnessMinimum + Math.Clamp(percent, 0, 100) / 100.0 *
            (BrightnessMaximum - BrightnessMinimum));
        if (!SetMonitorBrightness(_handle, value)) return false;
        BrightnessCurrent = value;
        return true;
    }

    public bool SetContrastPercent(double percent)
    {
        if (!HasContrast) return false;
        var value = (uint)Math.Round(ContrastMinimum + Math.Clamp(percent, 0, 100) / 100.0 *
            (ContrastMaximum - ContrastMinimum));
        if (!SetMonitorContrast(_handle, value)) return false;
        ContrastCurrent = value;
        return true;
    }

    public double BrightnessPercent => Percent(BrightnessCurrent, BrightnessMinimum, BrightnessMaximum);
    public double ContrastPercent => Percent(ContrastCurrent, ContrastMinimum, ContrastMaximum);

    private static double Percent(uint value, uint minimum, uint maximum) =>
        maximum <= minimum ? 0 : Math.Clamp((value - minimum) * 100.0 / (maximum - minimum), 0, 100);

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        try
        {
            DestroyPhysicalMonitors(1, new[]
            {
                new PHYSICAL_MONITOR { Handle = _handle, Description = Description }
            });
        }
        catch (Exception)
        {
            // Closing a DDC handle must never crash the app.
        }
        _handle = IntPtr.Zero;
    }

    private static IntPtr FindMonitor(string gdiDeviceName)
    {
        var found = IntPtr.Zero;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
        {
            var info = new MONITORINFOEX { Size = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(monitor, ref info) &&
                string.Equals(info.DeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                found = monitor;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count,
        [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint count, [In] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorContrast(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorContrast(IntPtr monitor, uint value);
}
