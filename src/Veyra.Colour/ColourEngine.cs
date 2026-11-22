using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Colour.AMD;
using Veyra.Colour.Intel;
using Veyra.Colour.NVIDIA;
using Veyra.Colour.Providers;

namespace Veyra.Colour;

/// <summary>
/// Combines the vendor-neutral Windows ramp provider with real per-display driver
/// saturation: AMD ADL2 colour type 4 or NVIDIA NVAPI Digital Vibrance Control.
/// Brightness, contrast, gamma and temperature remain separate Windows ramp controls.
/// </summary>
public sealed partial class ColourEngine : IDisposable
{
    private readonly WindowsGammaColourProvider _gamma = new();
    private readonly AdlSaturationService _amdSaturation = new();
    private readonly NvidiaSaturationService _nvidiaSaturation = new();
    private readonly ISaturationProvider[] _saturationProviders;
    private readonly NvidiaColourProvider _nvidia = new();
    private readonly IntelColourProvider _intel = new();
    private readonly IColourRecoveryStore _recovery;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ColourRecoverySnapshot _snapshot;

    public ColourEngine(IColourRecoveryStore recovery)
    {
        _recovery = recovery;
        _snapshot = recovery.Load();
        _saturationProviders = new ISaturationProvider[] { _amdSaturation, _nvidiaSaturation };
    }

    public string VendorDetail
    {
        get
        {
            var available = _saturationProviders.Where(p => p.IsAvailable).Select(p => p.Detail).ToArray();
            if (available.Length > 0)
            {
                return string.Join(" ", available) +
                       " Brightness, contrast, gamma and temperature stay on the Windows ramp.";
            }

            return "Driver saturation is unavailable. " +
                   string.Join(" ", _saturationProviders.Select(p => p.Detail)) +
                   " Brightness, contrast, gamma and temperature still use the Windows ramp.";
        }
    }

    public bool VendorWritesSupported => _saturationProviders.Any(p => p.IsAvailable);

    public IReadOnlyList<ColourProviderInfo> Providers => new[]
    {
        new ColourProviderInfo(_gamma.ProviderName, "Any", true, _gamma.AvailabilityDetail),
        new ColourProviderInfo(_amdSaturation.ProviderName, "AMD", _amdSaturation.IsAvailable, _amdSaturation.Detail),
        new ColourProviderInfo(_nvidiaSaturation.ProviderName, "NVIDIA", _nvidiaSaturation.IsAvailable, _nvidiaSaturation.Detail),
        new ColourProviderInfo(_intel.ProviderName, "Intel", _intel.IsAvailable, _intel.AvailabilityDetail)
    };

    public async Task<ColourCapabilities> GetCapabilitiesAsync(DisplayInfo display, CancellationToken cancellationToken)
    {
        var gamma = await _gamma.GetCapabilitiesAsync(display.GdiDeviceName, cancellationToken);
        var saturationProvider = SelectSaturationProvider(display);

        var saturation = saturationProvider?.GetSaturationCapability(display.GdiDeviceName)
            ?? ColourControlCapability.Unsupported(ColourControl.Saturation,
                SaturationUnavailableReason(), "none");

        var controls = gamma.Controls
            .Where(c => c.Control != ColourControl.Saturation)
            .Append(saturation)
            .ToArray();

        var summary = saturationProvider is null
            ? $"{gamma.ProviderName} active. {SaturationUnavailableReason()}"
            : "Ramp controls plus real " + saturationProvider.ProviderName + " saturation for this display.";

        return new ColourCapabilities(_gamma.ProviderName, display.VendorId ?? "Any", display.Id, controls, summary);
    }

    public async Task<ColourState> GetCurrentStateAsync(DisplayInfo display, CancellationToken cancellationToken)
    {
        var state = await _gamma.GetCurrentStateAsync(display.GdiDeviceName, cancellationToken);
        var saturationProvider = SelectSaturationProvider(display);
        var saturation = saturationProvider is null
            ? null
            : await saturationProvider.GetSaturationAsync(display.GdiDeviceName, cancellationToken);

        return saturation is null
            ? state
            : state with { Values = state.Values with { Saturation = saturation } };
    }

    /// <summary>Opens the driver APIs, maps displays, and restores anything a crash left behind.</summary>
    public async Task<OperationResult> InitializeAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_saturationProviders.Select(provider =>
            provider.InitializeAsync(cancellationToken)));

        var available = _saturationProviders.Where(p => p.IsAvailable).Select(p => p.Detail).ToArray();
        return available.Length > 0
            ? OperationResult.Success(string.Join(" ", available))
            : OperationResult.Unsupported(string.Join(" ", _saturationProviders.Select(p => p.Detail)));
    }

    /// <summary>Live saturation change on this display through its GPU driver.</summary>
    public Task<OperationResult> ApplySaturationAsync(DisplayInfo display, double percent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);
        var provider = SelectSaturationProvider(display);
        return provider is null
            ? Task.FromResult(OperationResult.Unsupported(SaturationUnavailableReason()))
            : provider.ApplyAsync(display.GdiDeviceName, percent, cancellationToken);
    }

    /// <summary>One-click bypass: restores this display's original driver saturation.</summary>
    public Task<OperationResult> RestoreSaturationAsync(DisplayInfo display, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);
        var provider = SelectSaturationProvider(display);
        return provider is null
            ? Task.FromResult(OperationResult.Success("No driver saturation change to restore."))
            : provider.RestoreAsync(display.GdiDeviceName, cancellationToken);
    }

    /// <summary>Live Windows-ramp write for the non-driver controls, without touching saturation.</summary>
    public async Task<OperationResult> ApplyRampAsync(DisplayInfo display, ColourSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CaptureOriginalsAsync(display, cancellationToken);
            return await _gamma.ApplyAsync(display.GdiDeviceName, settings, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> ApplyAsync(DisplayInfo display, ColourSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CaptureOriginalsAsync(display, cancellationToken);
            var ramp = await _gamma.ApplyAsync(display.GdiDeviceName, settings, cancellationToken);
            var requestedSaturation = settings.Saturation ?? 100;
            var saturationProvider = SelectSaturationProvider(display);

            var saturation = saturationProvider is not null
                ? await saturationProvider.ApplyAsync(display.GdiDeviceName, requestedSaturation, cancellationToken)
                : Math.Abs(requestedSaturation - 100) < 0.001
                    ? OperationResult.Success("Saturation is neutral.")
                    : OperationResult.Unsupported(SaturationUnavailableReason());

            if (!ramp.IsSuccess) return ramp;
            if (!saturation.IsSuccess) return saturation;

            return OperationResult.Success("Colour settings applied to " + display.FriendlyName + ".");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> RestoreAsync(DisplayInfo display, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var gamma = await _gamma.RestoreAsync(display.GdiDeviceName, cancellationToken);
            var saturationProvider = SelectSaturationProvider(display);
            var saturation = saturationProvider is null
                ? OperationResult.Success("No driver saturation change to restore.")
                : await saturationProvider.RestoreAsync(display.GdiDeviceName, cancellationToken);

            var remaining = _snapshot.Entries
                .Where(e => !string.Equals(e.DisplayId, display.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            _snapshot = _snapshot with { Entries = remaining, Dirty = remaining.Length > 0 };
            _recovery.Save(_snapshot);

            if (!gamma.IsSuccess) return gamma;
            if (!saturation.IsSuccess) return saturation;

            return OperationResult.Success("Original colour settings restored for " + display.FriendlyName + ".");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Re-applies any originals left dirty by a crash or an unclean exit.</summary>
    public async Task<OperationResult> RecoverPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _snapshot;
        if (!pending.Dirty || pending.Entries.Count == 0)
        {
            return OperationResult.Success("No colour recovery was needed.");
        }

        var failures = new List<string>();
        foreach (var entry in pending.Entries)
        {
            if (string.IsNullOrEmpty(entry.GammaRampBase64)) continue;

            var gdi = Veyra.Display.Monitors.DisplayIds.ResolveGdiName(entry.DisplayId) ?? entry.DisplayId;
            var restore = await _gamma.RestoreRawSnapshotAsync(gdi, entry.GammaRampBase64, cancellationToken);
            if (!restore.IsSuccess) failures.Add(restore.Message);
        }

        _snapshot = _snapshot with { Dirty = false };
        _recovery.Save(_snapshot);

        return failures.Count == 0
            ? OperationResult.Success("Recovered the original colour settings from the previous session.")
            : OperationResult.Failure("Colour recovery reported problems: " + string.Join("; ", failures));
    }

    private ISaturationProvider? SelectSaturationProvider(DisplayInfo display) =>
        _saturationProviders.FirstOrDefault(p => p.SupportsDisplay(display.GdiDeviceName));

    private string SaturationUnavailableReason() =>
        "No AMD ADL2 or NVIDIA NVAPI saturation control is mapped to this display. " +
        string.Join(" ", _saturationProviders.Select(p => p.Detail));

    public void Dispose()
    {
        foreach (var provider in _saturationProviders) provider.Dispose();
        _gate.Dispose();
    }
}
