using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Core.Services;

namespace Veyra.Colour.Providers;

/// <summary>
/// Vendor-neutral ramp provider. Every control here is a real GDI ramp adjustment applied
/// to the display output, and the range shown is the range Veyra can honour rather than a
/// marketing maximum.
/// </summary>
public sealed partial class WindowsGammaColourProvider : IColourProvider, IColourSnapshotProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ushort[]> _originals = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "Windows gamma ramp";
    public string Vendor => "Any";
    public bool IsAvailable => true;
    public string AvailabilityDetail =>
        "Vendor-neutral GDI ramp adjustments. Exclusive-fullscreen or HDR games may bypass the ramp.";

    public Task<bool> SupportsDisplayAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(displayId));

    public Task<ColourCapabilities> GetCapabilitiesAsync(string displayId, CancellationToken cancellationToken)
    {
        var controls = new[]
        {
            ColourControlCapability.Supported(ColourControl.Brightness, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Contrast, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Gamma, 0.5, 3.0, 0.01, 1.0, "gamma", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Temperature, 4000, 10000, 100, 6500, "K", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Red, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Green, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Blue, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.Shadow, -100, 100, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Supported(ColourControl.BlackLevel, 0, 50, 1, 0, "%", "GDI ramp"),
            ColourControlCapability.Unsupported(ColourControl.Hue,
                "Windows has no vendor-neutral hue control; a vendor driver API is required.")
        };

        return Task.FromResult(new ColourCapabilities(
            ProviderName, Vendor, displayId, controls,
            "Ramp controls apply to the desktop and to windowed or borderless games. Exclusive fullscreen and HDR surfaces can bypass the ramp."));
    }

    public Task<ColourState> GetCurrentStateAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var ramp = ReadRamp(ResolveDevice(displayId));
            var values = ramp is null ? new ColourSettings { Gamma = 1, Temperature = 6500 } : EstimateFromRamp(ramp);
            return new ColourState(displayId, values, ProviderName);
        }, cancellationToken);

    public Task<OperationResult> ApplyAsync(string displayId, ColourSettings settings, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var device = ResolveDevice(displayId);
            var open = Open(device, out var error);
            if (open == IntPtr.Zero) return OperationResult.Failure(error);

            try
            {
                lock (_sync)
                {
                    if (!_originals.ContainsKey(device))
                    {
                        var original = new ushort[GammaInterop.RampSize * 3];
                        if (!GammaInterop.GetDeviceGammaRamp(open, original))
                        {
                            return OperationResult.DriverUnavailable(
                                "The display driver refused to return its current gamma ramp.");
                        }

                        _originals[device] = original;
                    }
                }

                var ramp = ColourRampCalculator.BuildRamp(settings);
                if (!GammaInterop.SetDeviceGammaRamp(open, ramp))
                {
                    return OperationResult.DriverUnavailable(
                        "The display driver rejected the gamma ramp; some drivers limit the range they accept.");
                }

                var readBack = new ushort[GammaInterop.RampSize * 3];
                if (!GammaInterop.GetDeviceGammaRamp(open, readBack))
                {
                    return OperationResult.Failure("The ramp was written but could not be read back for verification.");
                }

                return RampsClose(ramp, readBack)
                    ? OperationResult.Success("Colour ramp applied.")
                    : OperationResult.Failure(
                        "The driver accepted the ramp but reports a different ramp, so the change is not applied as requested.");
            }
            finally
            {
                GammaInterop.DeleteDC(open);
            }
        }, cancellationToken);
}
