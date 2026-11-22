using System.Runtime.InteropServices;

namespace Veyra.Colour.Providers;

/// <summary>
/// GDI gamma ramp access. This is the documented, vendor-neutral path that changes the
/// display output ramp; it is not a vendor "digital vibrance" control and is labelled
/// as a ramp adjustment in the UI.
/// </summary>
internal static class GammaInterop
{
    public const int RampSize = 256;
    public const int RampBytes = RampSize * 3 * sizeof(ushort);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateDC(string? driver, string device, string? port, IntPtr initData);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetDeviceGammaRamp(IntPtr hdc, [Out] ushort[] ramp);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

    public static IntPtr OpenDevice(string gdiDeviceName) =>
        CreateDC(null, gdiDeviceName, null, IntPtr.Zero);
}
