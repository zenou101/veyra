using System.Text.Json;
using Veyra.Core.Models;
using Veyra.Core.Services;

namespace Veyra.Colour.AMD;

/// <summary>
/// Real per-display saturation through AMD's driver (ADL2 colour type 4), using the display mapping
/// VibranceGUI v2 uses: match the adapter that names the GDI display, enumerate its displays, and
/// write only a display that reports itself connected and mapped. The driver's exact prior native
/// value is captured per display, persisted before the first write, and restored on request or after
/// a crash. This changes the GPU output, not a gamma ramp, so it also survives exclusive-fullscreen.
/// </summary>
public sealed class AdlSaturationService : ISaturationProvider
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veyra", "adl-saturation-recovery.json");

    private sealed record Target(int AdapterIndex, int DisplayIndex, string GdiName, int Minimum, int Neutral, int Maximum, int Step, int Current);

    private readonly object _sync = new();
    private readonly Dictionary<string, Target> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _originals = new(StringComparer.OrdinalIgnoreCase);

    private AdlSession? _session;
    private bool _initialised;
    private string _detail = "AMD ADL saturation has not been initialised.";

    public string ProviderName => "AMD ADL2";
    public bool IsAvailable { get; private set; }
    public string Detail => _detail;

    public bool SupportsDisplay(string displayId) => FindTarget(displayId) is not null;

    public ColourControlCapability GetSaturationCapability(string displayId)
    {
        var target = FindTarget(displayId);
        return target is null
            ? ColourControlCapability.Unsupported(ColourControl.Saturation,
                "This display has no mapped AMD ADL2 saturation control.", ProviderName)
            : ColourControlCapability.Supported(ColourControl.Saturation,
                SaturationScale.MinimumPercent, SaturationScale.MaximumPercent, 1,
                SaturationScale.NeutralPercent, "%",
                "AMD ADL2 driver saturation (per display; 300 % verified extended ceiling)");
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return EnsureInitialised();
        }, cancellationToken);

    private bool EnsureInitialised()
    {
        lock (_sync)
        {
            if (_initialised) return IsAvailable;
            _initialised = true;

            _session = AdlSession.TryOpen(out var error);
            if (_session is null)
            {
                _detail = error;
                return false;
            }

            var adapters = _session.GetAdapters(out error);
            if (adapters.Count == 0)
            {
                _detail = error.Length > 0 ? error : "ADL reported no present adapters.";
                return false;
            }

            foreach (var gdi in SafeGdiNames())
            {
                // Each ADL adapter is named after the GDI display it drives. Adapter 0 plus a
                // parsed GDI number is not a valid mapping on this driver and makes writes fail.
                var adapter = adapters.FirstOrDefault(a =>
                    string.Equals(a.DisplayName, gdi, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(adapter.DisplayName)) continue;

                foreach (var display in _session.GetDisplays(adapter.AdapterIndex, adapter.AdapterName, out _))
                {
                    if (display.LogicalAdapterIndex != adapter.AdapterIndex) continue;
                    if ((display.InfoValue & 3) != 3) continue; // connected + mapped

                    var status = _session.GetColour(display.AdapterIndex, display.DisplayIndex,
                        AdlNative.ColorSaturation, out var current, out var neutral, out var minimum, out var maximum, out var step);
                    if (status != AdlNative.AdlOk || maximum <= minimum) continue;
                    if (neutral < minimum || neutral > maximum) continue;

                    _targets[gdi] = new Target(display.AdapterIndex, display.DisplayIndex, gdi,
                        minimum, neutral, maximum, Math.Max(step, 1), current);
                    break;
                }
            }

            if (_targets.Count == 0)
            {
                _detail = "ADL is present but no connected display exposes a writable saturation control.";
                return false;
            }

            IsAvailable = true;
            _detail = $"AMD driver saturation is available for {_targets.Count} display(s) through ADL2.";
            RecoverSavedOriginals();
            return true;
        }
    }

    private static IEnumerable<string> SafeGdiNames()
    {
        try
        {
            return Veyra.Display.Monitors.DisplayService.EnumerateDisplays()
                .Select(d => d.GdiDeviceName)
                .ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    public Task<OperationResult> ApplyAsync(string displayId, double percent, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _session is null) return OperationResult.Unsupported(_detail);
                var target = FindTarget(displayId);
                if (target is null) return OperationResult.Unsupported("This display has no AMD driver saturation control.");

                // Save the exact native value before the first change, so restore and crash
                // recovery never depend on the driver's default.
                if (!_originals.ContainsKey(target.GdiName))
                {
                    if (!Read(target, out var current)) return OperationResult.DriverUnavailable("The AMD driver stopped reporting saturation.");
                    _originals[target.GdiName] = current;
                    SaveState();
                }

                var native = SaturationScale.ToNative(percent, target.Minimum, target.Neutral, target.Maximum,
                    target.Step, SaturationScale.MaximumPercent);
                if (!WriteAndVerify(target, native))
                {
                    return OperationResult.DriverUnavailable("The AMD driver rejected the saturation change.");
                }

                return OperationResult.Success($"Saturation set to {SaturationScale.ClampPercent(percent):0} % through the AMD driver.");
            }
        }, cancellationToken);

    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _session is null) return OperationResult.Unsupported(_detail);
                var target = FindTarget(displayId);
                if (target is null) return OperationResult.Unsupported("This display has no AMD driver saturation control.");

                if (!_originals.TryGetValue(target.GdiName, out var original))
                {
                    return OperationResult.Success("The AMD saturation value was not changed by Veyra.");
                }

                if (!WriteAndVerify(target, original))
                {
                    return OperationResult.Failure("The AMD driver rejected the original saturation value.");
                }

                _originals.Remove(target.GdiName);
                SaveState();
                return OperationResult.Success("Original AMD saturation restored.");
            }
        }, cancellationToken);

    /// <summary>Current saturation as a percentage, or null when ADL cannot report it.</summary>
    public Task<double?> GetSaturationAsync(string displayId, CancellationToken cancellationToken = default) =>
        Task.Run<double?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _session is null) return null;
                var target = FindTarget(displayId);
                if (target is null || !Read(target, out var current)) return null;
                return SaturationScale.ToPercent(current, target.Minimum, target.Neutral, target.Maximum,
                    SaturationScale.MaximumPercent);
            }
        }, cancellationToken);

    private Target? FindTarget(string displayId)
    {
        var gdi = Providers.WindowsGammaColourProvider.NormaliseDevice(displayId);
        return _targets.TryGetValue(gdi, out var target) ? target
            : _targets.TryGetValue(displayId, out target) ? target
            : null;
    }

    private bool Read(Target target, out int current)
    {
        current = target.Current;
        if (_session is null) return false;
        var status = _session.GetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation,
            out var value, out _, out _, out _, out _);
        if (status != AdlNative.AdlOk) return false;
        current = value;
        return true;
    }

    private bool WriteAndVerify(Target target, int value)
    {
        if (_session is null) return false;

        var write = _session.SetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation, value);
        if (write != AdlNative.AdlOk) return false;

        var verify = _session.GetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation,
            out var after, out _, out _, out _, out _);
        if (verify != AdlNative.AdlOk || after != value) return false;

        _targets[target.GdiName] = target with { Current = value };
        return true;
    }

    private void RecoverSavedOriginals()
    {
        try
        {
            if (!File.Exists(StatePath)) return;

            var state = JsonSerializer.Deserialize<RecoveryState>(File.ReadAllText(StatePath));
            if (state?.Values is null)
            {
                File.Delete(StatePath);
                return;
            }

            foreach (var (gdi, value) in state.Values)
            {
                var target = FindTarget(gdi);
                if (target is null) continue;
                WriteAndVerify(target, value);
            }

            File.Delete(StatePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Recovery is best-effort; a damaged file must not stop the app from starting.
        }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            if (_originals.Count == 0)
            {
                File.Delete(StatePath);
                return;
            }

            var json = JsonSerializer.Serialize(new RecoveryState(true, new Dictionary<string, int>(_originals)));
            var temp = StatePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, StatePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory original still restores this session; only crash recovery is weaker.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _targets.Clear();
            _originals.Clear();
            _session = null;
        }
    }

    private sealed record RecoveryState(bool Dirty, Dictionary<string, int> Values);
}
