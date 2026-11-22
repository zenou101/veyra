using System.Runtime.InteropServices;
using System.Text;

namespace Veyra.Display.Monitors;

/// <summary>Reads the ICC profile currently associated with a display device.</summary>
internal static class ColourProfileReader
{
    private const int MaxPath = 260;

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDC(string? driver, string device, string? port, IntPtr initData);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", EntryPoint = "GetICMProfileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetICMProfile(IntPtr hdc, ref uint size, StringBuilder name);

    public static string? GetProfileName(string gdiDeviceName)
    {
        var hdc = CreateDC(null!, gdiDeviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;

        try
        {
            var size = (uint)MaxPath;
            var builder = new StringBuilder(MaxPath);
            if (!GetICMProfile(hdc, ref size, builder)) return null;

            var path = builder.ToString();
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
        }
        finally
        {
            DeleteDC(hdc);
        }
    }
}
