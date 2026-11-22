using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour;

/// <summary>
/// A real per-display saturation control exposed by a GPU driver. Implementations must
/// verify writes by reading the native value back and must preserve the original value.
/// </summary>
internal interface ISaturationProvider : IDisposable
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    string Detail { get; }

    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    bool SupportsDisplay(string displayId);
    ColourControlCapability GetSaturationCapability(string displayId);
    Task<double?> GetSaturationAsync(string displayId, CancellationToken cancellationToken = default);
    Task<OperationResult> ApplyAsync(string displayId, double percent, CancellationToken cancellationToken = default);
    Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken = default);
}
