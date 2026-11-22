using System.Runtime.InteropServices;
using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour.Providers;

public sealed partial class WindowsGammaColourProvider
{
    public Task<string?> CaptureRawSnapshotAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var ramp = ReadRamp(ResolveDevice(displayId));
            return ramp is null ? null : Convert.ToBase64String(MemoryMarshal.AsBytes(ramp.AsSpan()).ToArray());
        }, cancellationToken);

    public Task<OperationResult> RestoreRawSnapshotAsync(string displayId, string raw, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(raw);
            }
            catch (FormatException)
            {
                return OperationResult.Invalid("The stored gamma snapshot is not valid.");
            }

            if (bytes.Length != GammaInterop.RampBytes)
            {
                return OperationResult.Invalid("The stored gamma snapshot has an unexpected size.");
            }

            var ramp = new ushort[GammaInterop.RampSize * 3];
            Buffer.BlockCopy(bytes, 0, ramp, 0, bytes.Length);

            var open = Open(ResolveDevice(displayId), out var error);
            if (open == IntPtr.Zero) return OperationResult.Failure(error);

            try
            {
                return GammaInterop.SetDeviceGammaRamp(open, ramp)
                    ? OperationResult.Success("Original gamma ramp restored from the recovery snapshot.")
                    : OperationResult.Failure("The driver rejected the stored gamma ramp.");
            }
            finally
            {
                GammaInterop.DeleteDC(open);
            }
        }, cancellationToken);

    private static ColourSettings EstimateFromRamp(ushort[] ramp)
    {
        var mid = ramp[GammaInterop.RampSize / 2];
        var estimate = Math.Clamp(1.0 - (mid / 65535.0 - 0.5) * 2.0, 0.5, 3.0);
        return new ColourSettings { Gamma = Math.Round(estimate, 2), Temperature = 6500 };
    }
}
