using Veyra.Core.Models;

namespace Veyra.Display.Monitors;

public sealed partial class DisplayService
{
    public Task<OperationResult> ApplyModeAsync(DisplayInfo display, DisplayMode mode, CancellationToken cancellationToken) =>
        Task.Run(() => DisplayModeApplier.Apply(display, mode), cancellationToken);

    public Task<OperationResult> RestoreStateAsync(DisplayState state, CancellationToken cancellationToken) =>
        Task.Run(() => DisplayModeApplier.Restore(state), cancellationToken);

    public Task<OperationResult> SetHdrAsync(DisplayInfo display, bool enabled, CancellationToken cancellationToken) =>
        Task.Run(() => DisplayModeApplier.SetHdr(display, enabled), cancellationToken);
}
