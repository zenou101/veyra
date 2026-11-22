using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Core.Services;

namespace Veyra.Colour.NVIDIA;

/// <summary>
/// Real NVIDIA Digital Vibrance Control saturation through NVAPI. Each active NVIDIA
/// display is matched by the GDI name NVAPI reports, the exact native DVC value is
/// captured before the first write, and every write is verified by reading it back.
/// NVAPI's native maximum is respected; NVIDIA does not expose AMD's extended saturation
/// range, so the UI maximum is derived from the driver's own DVC range.
/// </summary>
public sealed class NvidiaSaturationService : ISaturationProvider
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veyra", "nvidia-dvc-recovery.json");

    private sealed record Target(IntPtr DisplayHandle, string GdiName, int Minimum, int Neutral, int Maximum, int Current);

    private readonly object _sync = new();
    private readonly Dictionary<string, Target> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _originals = new(StringComparer.OrdinalIgnoreCase);

    private NvApiNative? _api;
    private bool _initialised;
    private string _detail = "NVIDIA NVAPI saturation has not been initialised.";

    public string ProviderName => "NVIDIA NVAPI";
    public bool IsAvailable { get; private set; }
    public string Detail => _detail;

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

            _api = NvApiNative.TryOpen(out var error);
            if (_api is null)
            {
                _detail = error;
                return false;
            }

            for (uint index = 0; index < 64; index++)
            {
                var status = _api.EnumerateDisplay(index, out var handle);
                if (status == NvApiNative.EndEnumeration) break;
                if (status != NvApiNative.Ok)
                {
                    _detail = $"NVAPI could not enumerate displays: {NvApiNative.DescribeStatus(status)}.";
                    break;
                }

                var displayName = _api.GetDisplayName(handle);
                if (string.IsNullOrWhiteSpace(displayName)) continue;

                if (!_api.TryGetDvcInfo(handle, out var info)) continue;
                if (info.DefaultLevel < info.MinimumLevel || info.DefaultLevel > info.MaximumLevel) continue;

                var gdi = Providers.WindowsGammaColourProvider.NormaliseDevice(displayName);
                _targets[gdi] = new Target(handle, gdi, info.MinimumLevel, info.DefaultLevel,
                    info.MaximumLevel, info.CurrentLevel);
            }

            if (_targets.Count == 0)
            {
                if (_detail.Length == 0)
                {
                    _detail = "NVAPI is present but no active NVIDIA display exposes a writable Digital Vibrance control.";
                }
                return false;
            }

            IsAvailable = true;
            _detail = $"NVIDIA NVAPI digital vibrance is available for {_targets.Count} display(s).";
            RecoverSavedOriginals();
            return true;
        }
    }

    public bool SupportsDisplay(string displayId) => EnsureInitialised() && FindTarget(displayId) is not null;

    public ColourControlCapability GetSaturationCapability(string displayId)
    {
        if (!EnsureInitialised()) return ColourControlCapability.Unsupported(ColourControl.Saturation, _detail, ProviderName);
        var target = FindTarget(displayId);
        if (target is null)
        {
            return ColourControlCapability.Unsupported(ColourControl.Saturation,
                "This display is not driven by an NVIDIA GPU with Digital Vibrance Control.", ProviderName);
        }

        var maximumPercent = SaturationScale.PercentAtNativeMaximum(target.Neutral, target.Maximum);
        var detail = maximumPercent > SaturationScale.NeutralPercent
            ? $"NVIDIA NVAPI digital vibrance (per display; native {target.Minimum}..{target.Maximum}, neutral {target.Neutral}; driver maximum)"
            : "NVIDIA NVAPI reports no positive Digital Vibrance range for this display.";
        return ColourControlCapability.Supported(ColourControl.Saturation,
            SaturationScale.MinimumPercent, maximumPercent, 1, SaturationScale.NeutralPercent, "%", detail);
    }

    public Task<double?> GetSaturationAsync(string displayId, CancellationToken cancellationToken = default) =>
        Task.Run<double?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _api is null) return null;
                var target = FindTarget(displayId);
                if (target is null || !Read(target, out var info)) return null;

                var maximumPercent = SaturationScale.PercentAtNativeMaximum(target.Neutral, target.Maximum);
                return SaturationScale.ToPercent(info.CurrentLevel, target.Minimum, target.Neutral,
                    target.Maximum, maximumPercent);
            }
        }, cancellationToken);

    public Task<OperationResult> ApplyAsync(string displayId, double percent, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _api is null) return OperationResult.Unsupported(_detail);
                var target = FindTarget(displayId);
                if (target is null) return OperationResult.Unsupported("This display has no NVIDIA Digital Vibrance control.");

                if (!Read(target, out var info))
                {
                    return OperationResult.DriverUnavailable("NVAPI stopped reporting the NVIDIA Digital Vibrance state.");
                }

                if (!_originals.ContainsKey(target.GdiName))
                {
                    _originals[target.GdiName] = info.CurrentLevel;
                    SaveState();
                }

                var maximumPercent = SaturationScale.PercentAtNativeMaximum(target.Neutral, target.Maximum);
                var requested = SaturationScale.ClampPercent(percent, maximumPercent);
                var native = SaturationScale.ToNative(requested, target.Minimum, target.Neutral,
                    target.Maximum, 1, maximumPercent);
                if (!WriteAndVerify(target, native))
                {
                    return OperationResult.DriverUnavailable("The NVIDIA driver rejected the Digital Vibrance change.");
                }

                var capped = requested < SaturationScale.ClampPercent(percent);
                return OperationResult.Success(
                    $"NVIDIA digital vibrance set to {requested:0} % (native {native})" +
                    (capped ? $"; this display's driver maximum is {maximumPercent:0} %." : "."));
            }
        }, cancellationToken);

    public Task<OperationResult> RestoreAsync(string displayId, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!EnsureInitialised() || _api is null) return OperationResult.Unsupported(_detail);
                var target = FindTarget(displayId);
                if (target is null) return OperationResult.Unsupported("This display has no NVIDIA Digital Vibrance control.");

                if (!_originals.TryGetValue(target.GdiName, out var original))
                {
                    return OperationResult.Success("The NVIDIA Digital Vibrance value was not changed by Veyra.");
                }

                if (!WriteAndVerify(target, original))
                {
                    return OperationResult.Failure("The NVIDIA driver rejected the original Digital Vibrance value.");
                }

                _originals.Remove(target.GdiName);
                SaveState();
                return OperationResult.Success("Original NVIDIA Digital Vibrance restored.");
            }
        }, cancellationToken);

    private Target? FindTarget(string displayId)
    {
        var gdi = Providers.WindowsGammaColourProvider.NormaliseDevice(displayId);
        return _targets.TryGetValue(gdi, out var target) ? target
            : _targets.TryGetValue(displayId, out target) ? target
            : null;
    }

    private bool Read(Target target, out NvApiNative.DvcInfoEx info)
    {
        info = NvApiNative.DvcInfoEx.Create();
        if (_api is null || !_api.TryGetDvcInfo(target.DisplayHandle, out info)) return false;

        _targets[target.GdiName] = target with { Current = info.CurrentLevel };
        return true;
    }

    private bool WriteAndVerify(Target target, int value)
    {
        if (_api is null) return false;

        var write = _api.SetDvcLevel(target.DisplayHandle, value);
        if (write != NvApiNative.Ok) return false;

        if (!_api.TryGetDvcInfo(target.DisplayHandle, out var after)) return false;
        if (after.CurrentLevel != value) return false;

        _targets[target.GdiName] = target with { Current = value };
        return true;
    }

    private void RecoverSavedOriginals()
    {
        try
        {
            if (!File.Exists(StatePath)) return;

            var state = JsonSerializer.Deserialize<RecoveryState>(File.ReadAllText(StatePath));
            if (state?.Values is null || state.Values.Count == 0)
            {
                File.Delete(StatePath);
                return;
            }

            var failures = 0;
            foreach (var (gdi, value) in state.Values)
            {
                var target = FindTarget(gdi);
                if (target is null || !WriteAndVerify(target, value)) failures++;
            }

            if (failures == 0)
            {
                File.Delete(StatePath);
            }
            else
            {
                _detail += " A saved NVIDIA saturation restore is still pending.";
            }
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
            _api?.Dispose();
            _api = null;
        }
    }

    private sealed record RecoveryState(bool Dirty, Dictionary<string, int> Values);
}
