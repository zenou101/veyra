using Veyra.Core.Models;

namespace Veyra.Colour;

public sealed partial class ColourEngine
{
    private async Task CaptureOriginalsAsync(DisplayInfo display, CancellationToken cancellationToken)
    {
        var alreadyCaptured = _snapshot.Entries.Any(e =>
            string.Equals(e.DisplayId, display.Id, StringComparison.OrdinalIgnoreCase));

        if (alreadyCaptured) return;

        var ramp = await _gamma.CaptureRawSnapshotAsync(display.GdiDeviceName, cancellationToken);
        if (ramp is null) return;

        var entries = _snapshot.Entries.ToList();
        entries.Add(new ColourRecoveryEntry(display.Id, _gamma.ProviderName, ramp,
            new Dictionary<int, int>(), DateTimeOffset.UtcNow));

        _snapshot = _snapshot with { Entries = entries, Dirty = true };
        _recovery.Save(_snapshot);
    }
}
