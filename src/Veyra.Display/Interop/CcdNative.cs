using System.Runtime.InteropServices;

namespace Veyra.Display.Interop;

/// <summary>
/// Connecting and Configuring Displays (CCD) APIs. Veyra uses them read-only for
/// target identity, friendly names and HDR capability; mode changes go through
/// ChangeDisplaySettingsEx so a single, well-understood path is used for recovery.
/// </summary>
internal static class CcdNative
{
    public const uint QdcOnlyActivePaths = 0x00000002;

    public const int GetSourceName = 1;
    public const int GetTargetName = 2;
    public const int GetAdvancedColorInfoType = 9;
    public const int SetAdvancedColorStateType = 10;

    public const uint DisplayConfigPathActive = 0x00000001;
    public const uint DisplayConfigPathSourceInUse = 0x00000004;

    public const uint AdvancedColorSupported = 0x0001;
    public const uint AdvancedColorEnabled = 0x0002;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Rational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public Rational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PathInfo
    {
        public PathSourceInfo SourceInfo;
        public PathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    public struct ModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
        public ulong Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct GetAdvancedColorInfo
    {
        public DeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColor;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SetAdvancedColorState
    {
        public DeviceInfoHeader Header;
        public uint Value;
    }

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] PathInfo[] pathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] ModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName deviceName);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName deviceName);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static extern int DisplayConfigGetDeviceInfo(ref GetAdvancedColorInfo info);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")]
    public static extern int DisplayConfigSetDeviceInfo(ref SetAdvancedColorState state);

    public static int LastError => Marshal.GetLastWin32Error();
}
