using System.Runtime.InteropServices;

namespace Veyra.Colour.AMD;

/// <summary>
/// P/Invoke surface for the AMD Display Library that ships with the graphics driver
/// (atiadlxx.dll). Structures follow AMD's published adl_structures.h (ADL 18.x).
/// </summary>
internal static class AdlNative
{
    public const int AdlOk = 0;
    public const int AdlMaxPath = 256;

    public const int ColorBrightness = 1 << 0;
    public const int ColorContrast = 1 << 1;
    public const int ColorSaturation = 1 << 2;
    public const int ColorHue = 1 << 3;
    public const int ColorTemperature = 1 << 4;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr MallocCallback(int size);

    [DllImport("ucrtbase.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "malloc")]
    private static extern IntPtr UcrtMalloc(UIntPtr size);

    /// <summary>
    /// ADL frees its own allocations with the C runtime it was built against, so the
    /// callback must use the same allocator; AllocHGlobal here corrupts the process heap.
    /// </summary>
    public static IntPtr AllocateForAdl(int size) => UcrtMalloc((UIntPtr)(uint)Math.Max(size, 1));

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int ADL2_Main_Control_Create(MallocCallback callback, int enumConnectedAdapters, out IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Main_Control_Destroy(IntPtr context);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numberOfAdapters);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, IntPtr info, int inputSize);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Display_DisplayInfo_Get(
        IntPtr context, int adapterIndex, ref int numDisplays, out IntPtr displayInfo, int forceDetect);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Display_ColorCaps_Get(
        IntPtr context, int adapterIndex, int displayIndex, out int caps, out int valids);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Display_Color_Get(
        IntPtr context, int adapterIndex, int displayIndex, int colorType,
        out int current, out int defaultValue, out int minimum, out int maximum, out int step);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Display_Color_Set(
        IntPtr context, int adapterIndex, int displayIndex, int colorType, int current);

    [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct AdapterInfo
    {
        public int Size;
        public int AdapterIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string Udid;
        public int BusNumber;
        public int DeviceNumber;
        public int FunctionNumber;
        public int VendorId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string AdapterName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string DisplayName;
        public int Present;
        public int Exist;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string DriverPath;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string DriverPathExt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string PnpString;
        public int OsDisplayIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayId
    {
        public int LogicalIndex;
        public int PhysicalIndex;
        public int LogicalAdapterIndex;
        public int PhysicalAdapterIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DisplayInfo
    {
        public DisplayId DisplayId;
        public int ControllerIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AdlMaxPath)] public string ManufacturerName;
        public int DisplayType;
        public int OutputType;
        public int Connector;
        public int InfoMask;
        public int InfoValue;
    }

    public static int[] AllColourTypes { get; } =
    {
        ColorBrightness, ColorContrast, ColorSaturation, ColorHue, ColorTemperature
    };

    public static string DescribeColourType(int type) => type switch
    {
        ColorBrightness => "brightness",
        ColorContrast => "contrast",
        ColorSaturation => "saturation",
        ColorHue => "hue",
        ColorTemperature => "temperature",
        _ => "type " + type
    };
}
