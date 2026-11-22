using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour.Intel;

/// <summary>
/// Intel detection only. No Intel colour API is integrated in 1.0.0 and nothing is
/// claimed for Intel graphics.
/// </summary>
public sealed class IntelColourProvider : IColourProvider
{
    public string ProviderName => "Intel graphics";
    public string Vendor => "Intel";
    public bool IsAvailable => false;
    public string AvailabilityDetail =>
        "Intel display colour APIs are not integrated in 1.0.0. Ramp controls remain available.";

    public Task<bool> SupportsDisplayAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<ColourCapabilities> GetCapabilitiesAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(ColourCapabilities.None(Vendor, displayId,
            "Intel colour control is not implemented in this release; only vendor-neutral ramp controls are available."));

    public Task<ColourState> GetCurrentStateAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(new ColourState(displayId, new ColourSettings(), ProviderName));

    public Task<OperationResult> ApplyAsync(string displayId, ColourSettings settings, CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Unsupported("Intel colour control is not implemented in 1.0.0."));

    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Unsupported("Nothing was changed by the Intel provider, so there is nothing to restore."));
}
