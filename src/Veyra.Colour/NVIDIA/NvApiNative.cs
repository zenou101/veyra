using System.Runtime.InteropServices;
using System.Text;

namespace Veyra.Colour.NVIDIA;

/// <summary>
/// Minimal NVAPI surface for Digital Vibrance Control. NVAPI exposes one exported query
/// function; the remaining entry points are resolved by their stable function IDs. Only
/// the calls Veyra needs are bound.
/// </summary>
internal sealed class NvApiNative : IDisposable
{
    private const uint InitializeId = 0x0150E828;
    private const uint EnumNvidiaDisplayHandleId = 0x9ABDD40D;
    private const uint GetAssociatedNvidiaDisplayNameId = 0x22A78B05;
    private const uint GetDvcInfoExId = 0x0E45002D;
    private const uint SetDvcLevelExId = 0x4A82C2B1;

    public const int Ok = 0;
    public const int EndEnumeration = -7;
    public const int DeviceNotFound = -6;

    private const uint DvcInfoExVersion = 0x00010014; // structure size 20, version 1
    private const int DisplayNameLength = 64;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterfaceDelegate(uint functionId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumDisplayDelegate(uint index, out IntPtr displayHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int GetDisplayNameDelegate(IntPtr displayHandle, [Out] byte[] displayName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDvcInfoExDelegate(IntPtr displayHandle, uint outputId, ref DvcInfoEx info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetDvcLevelExDelegate(IntPtr displayHandle, uint outputId, ref DvcInfoEx info);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DvcInfoEx
    {
        public uint Version;
        public int CurrentLevel;
        public int MinimumLevel;
        public int MaximumLevel;
        public int DefaultLevel;

        public static DvcInfoEx Create() => new() { Version = DvcInfoExVersion };
    }

    private IntPtr _library;
    private QueryInterfaceDelegate? _queryInterface;
    private InitializeDelegate? _initialize;
    private EnumDisplayDelegate? _enumDisplays;
    private GetDisplayNameDelegate? _getDisplayName;
    private GetDvcInfoExDelegate? _getDvcInfo;
    private SetDvcLevelExDelegate? _setDvcLevel;

    private NvApiNative(IntPtr library)
    {
        _library = library;
    }

    public static string DriverPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll");

    public static NvApiNative? TryOpen(out string error)
    {
        error = string.Empty;
        var path = DriverPath;
        if (!File.Exists(path))
        {
            error = "nvapi64.dll was not found. Install the NVIDIA display driver to use digital vibrance.";
            return null;
        }

        NvApiNative? api = null;
        try
        {
            var library = NativeLibrary.Load(path);
            api = new NvApiNative(library);

            var queryPointer = NativeLibrary.GetExport(library, "nvapi_QueryInterface");
            api._queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryPointer);

            api._initialize = api.Resolve<InitializeDelegate>(InitializeId);
            api._enumDisplays = api.Resolve<EnumDisplayDelegate>(EnumNvidiaDisplayHandleId);
            api._getDisplayName = api.Resolve<GetDisplayNameDelegate>(GetAssociatedNvidiaDisplayNameId);
            api._getDvcInfo = api.Resolve<GetDvcInfoExDelegate>(GetDvcInfoExId);
            api._setDvcLevel = api.Resolve<SetDvcLevelExDelegate>(SetDvcLevelExId);

            var status = api._initialize();
            if (status != Ok)
            {
                error = $"NvAPI_Initialize returned {DescribeStatus(status)}.";
                api.Dispose();
                return null;
            }

            return api;
        }
        catch (DllNotFoundException)
        {
            error = "The NVIDIA driver runtime could not be loaded.";
        }
        catch (EntryPointNotFoundException)
        {
            error = "nvapi64.dll does not export nvapi_QueryInterface.";
        }
        catch (BadImageFormatException)
        {
            error = "nvapi64.dll could not be loaded into this x64 process.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            error = "NVAPI could not provide the digital vibrance entry points: " + ex.Message;
        }

        api?.Dispose();
        return null;
    }

    private T Resolve<T>(uint functionId) where T : Delegate
    {
        if (_queryInterface is null) throw new InvalidOperationException("NVAPI query interface is unavailable.");
        var pointer = _queryInterface(functionId);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"NVAPI function 0x{functionId:X8} is not implemented by this driver.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    public int EnumerateDisplay(uint index, out IntPtr displayHandle)
    {
        displayHandle = IntPtr.Zero;
        return _enumDisplays?.Invoke(index, out displayHandle) ?? DeviceNotFound;
    }

    public string? GetDisplayName(IntPtr displayHandle)
    {
        if (_getDisplayName is null) return null;
        var buffer = new byte[DisplayNameLength];
        var status = _getDisplayName(displayHandle, buffer);
        if (status != Ok) return null;

        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) length = buffer.Length;
        return Encoding.ASCII.GetString(buffer, 0, length).Trim();
    }

    public bool TryGetDvcInfo(IntPtr displayHandle, out DvcInfoEx info)
    {
        info = DvcInfoEx.Create();
        var status = _getDvcInfo?.Invoke(displayHandle, 0, ref info) ?? DeviceNotFound;
        return status == Ok && info.MaximumLevel > info.MinimumLevel &&
               info.DefaultLevel >= info.MinimumLevel && info.DefaultLevel <= info.MaximumLevel;
    }

    public int GetDvcInfo(IntPtr displayHandle, out DvcInfoEx info)
    {
        info = DvcInfoEx.Create();
        return _getDvcInfo?.Invoke(displayHandle, 0, ref info) ?? DeviceNotFound;
    }

    public int SetDvcLevel(IntPtr displayHandle, int level)
    {
        if (_setDvcLevel is null) return DeviceNotFound;

        // SetDVCLevelEx carries the complete range; the driver uses the currentLevel field.
        if (GetDvcInfo(displayHandle, out var info) != Ok) return DeviceNotFound;
        info.CurrentLevel = level;
        return _setDvcLevel(displayHandle, 0, ref info);
    }

    public static string DescribeStatus(int status) => status switch
    {
        Ok => "NVAPI_OK",
        -1 => "NVAPI_ERROR",
        -2 => "NVAPI_LIBRARY_NOT_FOUND",
        -3 => "NVAPI_NO_IMPLEMENTATION",
        -4 => "NVAPI_API_NOT_INITIALIZED",
        -5 => "NVAPI_INVALID_ARGUMENT",
        DeviceNotFound => "NVAPI_NVIDIA_DEVICE_NOT_FOUND",
        EndEnumeration => "NVAPI_END_ENUMERATION",
        -8 => "NVAPI_INVALID_HANDLE",
        -9 => "NVAPI_INCOMPATIBLE_STRUCTURE_VERSION",
        -10 => "NVAPI_HANDLE_INVALIDATED",
        -104 => "NVAPI_NOT_SUPPORTED",
        -108 => "NVAPI_DEVICE_BUSY",
        _ => "NVAPI status " + status
    };

    public void Dispose()
    {
        _queryInterface = null;
        _initialize = null;
        _enumDisplays = null;
        _getDisplayName = null;
        _getDvcInfo = null;
        _setDvcLevel = null;

        var library = _library;
        _library = IntPtr.Zero;
        if (library != IntPtr.Zero)
        {
            try { NativeLibrary.Free(library); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
        }
    }
}
