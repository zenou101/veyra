using System.Runtime.InteropServices;

namespace Veyra.Telemetry.Interop;

/// <summary>Minimal PDH wrapper used for system-wide GPU counters.</summary>
internal static class PdhInterop
{
    internal const uint ErrorSuccess = 0;
    internal const uint MoreData = 0x800007D2;
    internal const uint FormatDouble = 0x00000200;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FmtCounterValue
    {
        public uint Status;
        public double Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FmtCounterValueItem
    {
        public IntPtr Name;
        public FmtCounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    internal static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    internal static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    internal static extern uint PdhCloseQuery(IntPtr query);
}
