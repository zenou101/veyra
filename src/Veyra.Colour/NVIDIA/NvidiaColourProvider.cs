using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour.NVIDIA;

/// <summary>
/// NVIDIA detection metadata for the provider list. Saturation itself is implemented by
/// <see cref="NvidiaSaturationService"/> through NVAPI Digital Vibrance Control.
/// </summary>
public sealed class NvidiaColourProvider : IColourProvider
{
    private static readonly string DriverPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll");

    public string ProviderName => "NVIDIA NVAPI";
    public string Vendor => "NVIDIA";
    public bool IsAvailable => File.Exists(DriverPath);
    public string AvailabilityDetail => IsAvailable
        ? "The NVIDIA driver runtime is present. Digital vibrance is provided by the NVAPI saturation service."
        : "No NVIDIA driver runtime was found on this system.";

    public Task<bool> SupportsDisplayAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(IsAvailable);

    public Task<ColourCapabilities> GetCapabilitiesAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(ColourCapabilities.None(Vendor, displayId,
            IsAvailable
                ? "NVIDIA saturation is exposed per display by the NVAPI Digital Vibrance service."
                : "No NVIDIA driver runtime is present."));

    public Task<ColourState> GetCurrentStateAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(new ColourState(displayId, new ColourSettings(), ProviderName));

    public Task<OperationResult> ApplyAsync(string displayId, ColourSettings settings, CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Unsupported("NVIDIA colour control is not implemented in 1.0.0."));

    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Unsupported("Nothing was changed by the NVIDIA provider, so there is nothing to restore."));
}
