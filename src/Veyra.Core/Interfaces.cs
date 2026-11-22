using Veyra.Core.Models;

namespace Veyra.Core;

public interface IColourProvider
{
    string ProviderName { get; }
    string Vendor { get; }
    bool IsAvailable { get; }
    string AvailabilityDetail { get; }

    Task<bool> SupportsDisplayAsync(string displayId, CancellationToken cancellationToken);
    Task<ColourCapabilities> GetCapabilitiesAsync(string displayId, CancellationToken cancellationToken);
    Task<ColourState> GetCurrentStateAsync(string displayId, CancellationToken cancellationToken);
    Task<OperationResult> ApplyAsync(string displayId, ColourSettings settings, CancellationToken cancellationToken);
    Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken);
}

public interface IDisplayService
{
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken cancellationToken);
    Task<DisplayState?> CaptureStateAsync(string displayId, CancellationToken cancellationToken);
    Task<OperationResult> ApplyModeAsync(DisplayInfo display, DisplayMode mode, CancellationToken cancellationToken);
    Task<OperationResult> RestoreStateAsync(DisplayState state, CancellationToken cancellationToken);
    Task<OperationResult> SetHdrAsync(DisplayInfo display, bool enabled, CancellationToken cancellationToken);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IPresetRepository
{
    Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Preset?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(Preset preset, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IGameRepository
{
    Task<IReadOnlyList<GameProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GameProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IHotkeyRepository
{
    IReadOnlyList<HotkeyBinding> GetAll();
    void Save(IReadOnlyList<HotkeyBinding> bindings);
}

public interface IColourRecoveryStore
{
    ColourRecoverySnapshot Load();
    void Save(ColourRecoverySnapshot snapshot);
    void MarkClean();
}

public interface ITelemetryService
{
    TelemetryCapabilities Capabilities { get; }
    Task<TelemetrySample> SampleAsync(CancellationToken cancellationToken);
}

public interface IGameDetector : IDisposable
{
    event EventHandler<GameProcessChangedEventArgs>? ProcessChanged;
    IReadOnlyCollection<string> CurrentProcessNames { get; }
    void Start(TimeSpan interval);
    void Stop();
}

public sealed class GameProcessChangedEventArgs : EventArgs
{
    public GameProcessChangedEventArgs(string processName, bool started)
    {
        ProcessName = processName;
        Started = started;
    }

    public string ProcessName { get; }
    public bool Started { get; }
}

/// <summary>
/// Optional capability: providers that can export the exact driver state so it can be
/// persisted and restored after a crash, not merely recomputed from UI values.
/// </summary>
public interface IColourSnapshotProvider
{
    Task<string?> CaptureRawSnapshotAsync(string displayId, CancellationToken cancellationToken);
    Task<OperationResult> RestoreRawSnapshotAsync(string displayId, string raw, CancellationToken cancellationToken);
}
