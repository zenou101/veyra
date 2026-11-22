using System.Runtime.InteropServices;

namespace Veyra.Telemetry.Interop;

internal static class SystemMetrics
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GlobalMemoryStatusEx")]
    private static extern bool GlobalMemoryStatusExNative(ref MemoryStatusEx buffer);

    internal static bool TryReadCpu(out ulong idle, out ulong kernel, out ulong user)
    {
        var ok = GetSystemTimes(out var i, out var k, out var u);
        idle = i.Value;
        kernel = k.Value;
        user = u.Value;
        return ok;
    }

    internal static bool TryReadMemory(out double usedGb, out double totalGb, out double percent)
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusExNative(ref status))
        {
            usedGb = totalGb = percent = 0;
            return false;
        }

        totalGb = status.TotalPhys / 1024d / 1024d / 1024d;
        usedGb = (status.TotalPhys - status.AvailPhys) / 1024d / 1024d / 1024d;
        percent = status.MemoryLoad;
        return true;
    }
}
