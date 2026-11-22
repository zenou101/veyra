using System.Runtime.InteropServices;
using System.Windows;
using Veyra.Core.Models;

namespace Veyra.Overlay;

/// <summary>Owns overlay windows; created and disposed on the UI thread.</summary>
public sealed class OverlayService : IDisposable
{
    private readonly List<CrosshairOverlayWindow> _windows = new();

    public bool IsVisible => _windows.Count > 0;

    public CrosshairSettings Current { get; private set; } = CrosshairSettings.Default;

    public event EventHandler<bool>? VisibilityChanged;

    public void Show(CrosshairSettings settings, DisplayInfo? display)
    {
        Current = settings;

        if (_windows.Count > 0)
        {
            foreach (var window in _windows) window.Update(settings);
            return;
        }

        var bounds = MonitorLocator.BoundsFor(display?.GdiDeviceName);
        var overlay = new CrosshairOverlayWindow(settings, bounds);
        overlay.Show();
        _windows.Add(overlay);
        VisibilityChanged?.Invoke(this, true);
    }

    public void Update(CrosshairSettings settings)
    {
        Current = settings;
        if (!IsVisible) return;
        foreach (var window in _windows) window.Update(settings);
    }

    public void Hide()
    {
        if (!IsVisible) return;

        foreach (var window in _windows) window.Close();
        _windows.Clear();
        VisibilityChanged?.Invoke(this, false);
    }

    public void Toggle(CrosshairSettings settings, DisplayInfo? display)
    {
        if (IsVisible) Hide();
        else Show(settings, display);
    }

    public void Dispose() => Hide();
}

internal static class MonitorLocator
{
    private const int MonitorInfoPrimary = 1;

    public static Rect BoundsFor(string? gdiDeviceName)
    {
        var fallback = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        if (string.IsNullOrEmpty(gdiDeviceName))
        {
            return ToPhysical(fallback);
        }

        var target = NormaliseDevice(gdiDeviceName);
        Rect? found = null;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(monitor, ref info)) return true;

            var name = NormaliseDevice(info.Device);
            if (!string.Equals(name, target, StringComparison.OrdinalIgnoreCase)) return true;

            found = new Rect(info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
            return false;
        }, IntPtr.Zero);

        return found ?? ToPhysical(fallback);
    }

    private static string NormaliseDevice(string value) =>
        new string(value.Where(c => c != (char)92 && c != (char)46).ToArray());

    private static Rect ToPhysical(Rect rect) => rect;

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
}
