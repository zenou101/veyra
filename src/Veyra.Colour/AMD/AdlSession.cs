using System.Runtime.InteropServices;

namespace Veyra.Colour.AMD;

internal sealed record AdlDisplayTarget(
    int AdapterIndex, int DisplayIndex, string GdiName, string DisplayName, string Manufacturer,
    int InfoMask, int InfoValue, int LogicalAdapterIndex);

/// <summary>
/// Owns one ADL context and the adapter/display enumeration. Nothing is changed here;
/// callers must consult the reported ranges before writing a value.
/// </summary>
internal sealed class AdlSession : IDisposable
{
    private readonly AdlNative.MallocCallback _malloc;
    private IntPtr _context;

    private AdlSession(IntPtr context, AdlNative.MallocCallback malloc)
    {
        _context = context;
        _malloc = malloc;
    }

    public bool IsOpen => _context != IntPtr.Zero;

    public static AdlSession? TryOpen(out string error)
    {
        error = string.Empty;
        AdlNative.MallocCallback malloc = size => Marshal.AllocHGlobal(size);

        try
        {
            var status = AdlNative.ADL2_Main_Control_Create(malloc, 1, out var context);
            if (status != AdlNative.AdlOk || context == IntPtr.Zero)
            {
                error = $"ADL2_Main_Control_Create returned {status}.";
                return null;
            }

            return new AdlSession(context, malloc);
        }
        catch (DllNotFoundException)
        {
            error = "atiadlxx.dll was not found. The AMD driver does not expose ADL on this system.";
            return null;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = "The installed ADL runtime does not export the expected entry point: " + ex.Message;
            return null;
        }
        catch (BadImageFormatException)
        {
            error = "atiadlxx.dll could not be loaded into this process (architecture mismatch).";
            return null;
        }
    }

    public bool TryGetAdapterCount(out int count)
    {
        count = 0;
        return _context != IntPtr.Zero && AdlNative.ADL2_Adapter_NumberOfAdapters_Get(_context, out count) == AdlNative.AdlOk;
    }

    public IReadOnlyList<AdlNative.AdapterInfo> GetAdapters(out string error)
    {
        error = string.Empty;
        var adapters = new List<AdlNative.AdapterInfo>();
        if (_context == IntPtr.Zero)
        {
            error = "The ADL context is not open.";
            return adapters;
        }

        var status = AdlNative.ADL2_Adapter_NumberOfAdapters_Get(_context, out var count);
        if (status != AdlNative.AdlOk || count <= 0)
        {
            error = $"ADL reported {status} while reading the adapter count (count {count}).";
            return adapters;
        }

        var size = Marshal.SizeOf<AdlNative.AdapterInfo>();
        var buffer = Marshal.AllocHGlobal(size * count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var entry = new AdlNative.AdapterInfo
                {
                    Size = size,
                    Udid = string.Empty,
                    AdapterName = string.Empty,
                    DisplayName = string.Empty,
                    DriverPath = string.Empty,
                    DriverPathExt = string.Empty,
                    PnpString = string.Empty
                };
                Marshal.StructureToPtr(entry, buffer + i * size, false);
            }

            status = AdlNative.ADL2_Adapter_AdapterInfo_Get(_context, buffer, size * count);
            if (status != AdlNative.AdlOk)
            {
                error = $"ADL2_Adapter_AdapterInfo_Get returned {status}.";
                return adapters;
            }

            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<AdlNative.AdapterInfo>(buffer + i * size);
                if (info.Present != 0) adapters.Add(info);
            }

            return adapters;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public IReadOnlyList<AdlDisplayTarget> GetDisplays(int adapterIndex, string adapterName, out string error)
    {
        error = string.Empty;
        var displays = new List<AdlDisplayTarget>();
        if (_context == IntPtr.Zero)
        {
            error = "The ADL context is not open.";
            return displays;
        }

        var count = 0;
        var status = AdlNative.ADL2_Display_DisplayInfo_Get(_context, adapterIndex, ref count, out var pointer, 1);
        if (status != AdlNative.AdlOk || pointer == IntPtr.Zero || count <= 0)
        {
            error = $"ADL2_Display_DisplayInfo_Get returned {status} (count {count}).";
            return displays;
        }

        var size = Marshal.SizeOf<AdlNative.DisplayInfo>();
        for (var i = 0; i < count; i++)
        {
            var info = Marshal.PtrToStructure<AdlNative.DisplayInfo>(pointer + i * size);
            displays.Add(new AdlDisplayTarget(
                adapterIndex,
                info.DisplayId.LogicalIndex >= 0 ? info.DisplayId.LogicalIndex : i,
                info.DisplayName ?? string.Empty,
                $"{adapterName} display {i + 1}",
                info.ManufacturerName ?? string.Empty,
                info.InfoMask,
                info.InfoValue,
                info.DisplayId.LogicalAdapterIndex));
        }

        return displays;
    }

    public int GetColour(int adapterIndex, int displayIndex, int type,
        out int current, out int defaultValue, out int minimum, out int maximum, out int step) =>
        AdlNative.ADL2_Display_Color_Get(_context, adapterIndex, displayIndex, type,
            out current, out defaultValue, out minimum, out maximum, out step);

    public int SetColour(int adapterIndex, int displayIndex, int type, int value) =>
        AdlNative.ADL2_Display_Color_Set(_context, adapterIndex, displayIndex, type, value);

    public int GetColourCaps(int adapterIndex, int displayIndex, out int caps, out int valids) =>
        AdlNative.ADL2_Display_ColorCaps_Get(_context, adapterIndex, displayIndex, out caps, out valids);

    public bool TryGetTemperature(int adapterIndex, out int temperature)
    {
        temperature = 0;
        return _context != IntPtr.Zero &&
               AdlNative.ADL2_Overdrive6_Temperature_Get(_context, adapterIndex, out temperature) == AdlNative.AdlOk;
    }

    public void Dispose()
    {
        if (_context == IntPtr.Zero) return;
        try
        {
            // The context is left for process teardown: destroying it from managed code can
            // race the driver runtime during shutdown.
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // The driver went away; the context is released with the process anyway.
        }

        _context = IntPtr.Zero;
        GC.KeepAlive(_malloc);
    }
}

internal static class AdlSessionSafety
{
    // Reserved for diagnostics that need a count without touching the adapter-info buffer.
}
