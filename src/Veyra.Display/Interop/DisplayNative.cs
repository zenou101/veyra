using System.Runtime.InteropServices;

namespace Veyra.Display.Interop;

/// <summary>Documented Win32 display APIs used for enumeration and mode changes.</summary>
internal static class DisplayNative
{
    public const int EnumCurrentSettings = -1;
    public const uint EdsRawMode = 0x00000002;

    public const uint CdsUpdateRegistry = 0x00000001;
    public const uint CdsTest = 0x00000002;
    public const uint CdsFullscreen = 0x00000004;

    public const int DispChangeSuccessful = 0;
    public const int DispChangeRestart = 1;
    public const int DispChangeFailed = -1;
    public const int DispChangeBadMode = -2;
    public const int DispChangeNotUpdated = -3;
    public const int DispChangeBadFlags = -4;
    public const int DispChangeBadParam = -5;

    public const uint DmBitsPerPel = 0x00040000;
    public const uint DmPelsWidth = 0x00080000;
    public const uint DmPelsHeight = 0x00100000;
    public const uint DmDisplayFrequency = 0x00400000;
    public const uint DmPosition = 0x00000020;
    public const uint DmDisplayOrientation = 0x00000080;
    public const uint DmDisplayFixedOutput = 0x20000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public short LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint IcmMethod;
        public uint IcmIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public const uint DisplayDeviceAttachedToDesktop = 0x00000001;
    public const uint DisplayDevicePrimaryDevice = 0x00000004;
    public const uint DisplayDeviceMirroringDriver = 0x00000008;

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayDevices(string? device, uint deviceNumber, ref DisplayDevice displayDevice, uint flags);

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplaySettingsEx(string? deviceName, int modeNumber, ref DevMode devMode, uint flags);

    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(string? deviceName, ref DevMode devMode, IntPtr window, uint flags, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    public static DevMode CreateDevMode() => new()
    {
        DeviceName = string.Empty,
        FormName = string.Empty,
        Size = (short)Marshal.SizeOf<DevMode>()
    };
}
