using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Display.Monitors;

namespace Veyra.Colour.Providers;

public sealed partial class WindowsGammaColourProvider
{
    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var device = ResolveDevice(displayId);
            var open = Open(device, out var error);
            if (open == IntPtr.Zero) return OperationResult.Failure(error);

            try
            {
                ushort[]? original;
                lock (_sync)
                {
                    _originals.TryGetValue(device, out original);
                }

                original ??= BuildIdentityRamp();

                if (!GammaInterop.SetDeviceGammaRamp(open, original))
                {
                    return OperationResult.Failure("The original gamma ramp could not be restored.");
                }

                lock (_sync)
                {
                    _originals.Remove(device);
                }

                return OperationResult.Success("Original gamma ramp restored.");
            }
            finally
            {
                GammaInterop.DeleteDC(open);
            }
        }, cancellationToken);

    private static ushort[]? ReadRamp(string device)
    {
        var open = Open(device, out _);
        if (open == IntPtr.Zero) return null;

        try
        {
            var ramp = new ushort[GammaInterop.RampSize * 3];
            return GammaInterop.GetDeviceGammaRamp(open, ramp) ? ramp : null;
        }
        finally
        {
            GammaInterop.DeleteDC(open);
        }
    }

    private static IntPtr Open(string device, out string error)
    {
        if (string.IsNullOrWhiteSpace(device))
        {
            error = "The display device name is missing.";
            return IntPtr.Zero;
        }

        var hdc = GammaInterop.OpenDevice(device);
        error = hdc == IntPtr.Zero ? "A device context for the display could not be created." : string.Empty;
        return hdc;
    }

    /// <summary>Display ids may be a monitor path; only GDI device names can be opened.</summary>
    private static string ResolveDevice(string displayId) => NormaliseDevice(displayId);

    internal static string NormaliseDevice(string displayId)
    {
        if (string.IsNullOrWhiteSpace(displayId)) return string.Empty;
        var gdi = DisplayIds.ResolveGdiName(displayId);
        return string.IsNullOrEmpty(gdi) ? displayId : gdi;
    }

    private static bool RampsClose(ushort[] expected, ushort[] actual)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > 1024) return false;
        }

        return true;
    }

    private static ushort[] BuildIdentityRamp()
    {
        var ramp = new ushort[GammaInterop.RampSize * 3];
        for (var i = 0; i < GammaInterop.RampSize; i++)
        {
            var value = (ushort)(i * 65535 / 255);
            ramp[i] = value;
            ramp[GammaInterop.RampSize + i] = value;
            ramp[GammaInterop.RampSize * 2 + i] = value;
        }

        return ramp;
    }
}
