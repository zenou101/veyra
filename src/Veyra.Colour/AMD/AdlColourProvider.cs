using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Colour.AMD;

public sealed record AdlControlReport(
    int Type, string Name, bool Supported, int Current, int Default,
    int Minimum, int Maximum, int Step, int Status);

public sealed record AdlDiagnostics(
    bool Loaded, string Detail, IReadOnlyList<string> Lines, IReadOnlyList<AdlControlReport> Controls);

internal sealed record AdlTargetMapping(int AdapterIndex, int DisplayIndex, string GdiName, string DisplayName, int Caps, int Valids);

/// <summary>
/// Real AMD colour control through the driver's ADL runtime. Only controls the driver
/// reports as valid are exposed, with the driver's own minimum, maximum and step.
/// </summary>
public sealed partial class AdlColourProvider : IColourProvider, IColourSnapshotProvider, IDisposable
{
    private readonly object _sync = new();
    private readonly List<AdlTargetMapping> _targets = new();
    private readonly Dictionary<(int Adapter, int Display, int Type), int> _originals = new();
    private AdlSession? _session;
    private string _initError = string.Empty;
    private bool _initialised;

    public string ProviderName => "AMD ADL";
    public string Vendor => "AMD";

    public bool IsAvailable => EnsureInitialised() && _targets.Count > 0;

    public string AvailabilityDetail => _initError.Length > 0
        ? _initError
        : _targets.Count == 0
            ? "ADL reported no mapped AMD displays."
            : $"ADL is available with {_targets.Count} mapped display(s).";

    private bool EnsureInitialised()
    {
        lock (_sync)
        {
            if (_initialised) return _targets.Count > 0;
            _initialised = true;

            _session = AdlSession.TryOpen(out var openError);
            if (_session is null)
            {
                _initError = openError;
                return false;
            }

            if (!_session.TryGetAdapterCount(out var adapterCount) || adapterCount <= 0)
            {
                _initError = "ADL reported no present graphics adapters.";
                return false;
            }

            var adapters = _session.GetAdapters(out var adapterError);
            if (adapters.Count == 0)
            {
                _initError = adapterError.Length > 0 ? adapterError : "ADL reported no present adapters.";
                return false;
            }

            var gdiNames = SafeGdiNames();
            foreach (var gdi in gdiNames)
            {
                // ADL reports one logical adapter per display path. Matching the adapter's
                // GDI display name is what keeps later colour writes on the right display;
                // adapter 0 plus a parsed GDI number is not a valid mapping on this driver.
                var adapter = adapters.FirstOrDefault(a =>
                    string.Equals(a.DisplayName, gdi, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(adapter.DisplayName))
                {
                    adapter = adapters.FirstOrDefault(a => a.OsDisplayIndex == ParseDisplayIndex(gdi));
                }
                if (string.IsNullOrEmpty(adapter.DisplayName)) continue;

                var displays = _session.GetDisplays(adapter.AdapterIndex, adapter.AdapterName, out _);
                foreach (var display in displays)
                {
                    if (display.LogicalAdapterIndex != adapter.AdapterIndex) continue;
                    if ((display.InfoValue & 3) != 3) continue; // connected + mapped
                    if (display.AdapterIndex != adapter.AdapterIndex) continue;

                    if (_session.GetColourCaps(display.AdapterIndex, display.DisplayIndex, out var caps, out var valids) != AdlNative.AdlOk) continue;

                    var probe = _session.GetColour(display.AdapterIndex, display.DisplayIndex, AdlNative.ColorSaturation,
                        out _, out _, out var min, out var max, out _);
                    if (probe != AdlNative.AdlOk || max <= min) continue;

                    _targets.Add(new AdlTargetMapping(display.AdapterIndex, display.DisplayIndex, gdi,
                        string.IsNullOrWhiteSpace(display.DisplayName) ? "AMD display " + (display.DisplayIndex + 1) : display.DisplayName,
                        caps, valids));
                    break;
                }
            }

            if (_targets.Count == 0)
            {
                _initError = "ADL reported no usable colour target for the connected displays.";
            }

            return _targets.Count > 0;
        }
    }

    private static int ParseDisplayIndex(string gdiName)
    {
        var digits = new string(gdiName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) && number > 0 ? number - 1 : 0;
    }

    private static IReadOnlyList<string> SafeGdiNames()
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

    private static string? MapToGdi(AdlDisplayTarget display, AdlNative.AdapterInfo adapter,
        IReadOnlyList<string> gdiNames, HashSet<string> used)
    {
        if (!string.IsNullOrWhiteSpace(display.GdiName) &&
            display.GdiName.Contains("DISPLAY", StringComparison.OrdinalIgnoreCase) &&
            gdiNames.Contains(display.GdiName, StringComparer.OrdinalIgnoreCase) &&
            !used.Contains(display.GdiName))
        {
            return display.GdiName;
        }

        var named = gdiNames.FirstOrDefault(n =>
            n.EndsWith("DISPLAY" + (adapter.OsDisplayIndex + 1), StringComparison.OrdinalIgnoreCase) &&
            !used.Contains(n));

        if (named is not null) return named;

        var free = gdiNames.FirstOrDefault(n => !used.Contains(n));
        return free;
    }

    private AdlTargetMapping? FindTarget(string displayId)
    {
        if (!EnsureInitialised()) return null;

        var gdi = Veyra.Colour.Providers.WindowsGammaColourProvider.NormaliseDevice(displayId);
        return _targets.FirstOrDefault(t => string.Equals(t.GdiName, gdi, StringComparison.OrdinalIgnoreCase))
               ?? _targets.FirstOrDefault(t => string.Equals(t.GdiName, displayId, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> SupportsDisplayAsync(string displayId, CancellationToken cancellationToken) =>
        Task.FromResult(FindTarget(displayId) is not null);

    public Task<ColourCapabilities> GetCapabilitiesAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            if (target is null || _session is null) return ColourCapabilities.None(Vendor, displayId, AvailabilityDetail);

            var controls = new List<ColourControlCapability>();
            foreach (var type in AdlNative.AllColourTypes)
            {
                var status = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                    out _, out var def, out var min, out var max, out var step);

                if (status != AdlNative.AdlOk || max <= min)
                {
                    controls.Add(ColourControlCapability.Unsupported(MapControl(type),
                        $"The AMD driver reports no {AdlNative.DescribeColourType(type)} control for this display (ADL status {status})."));
                    continue;
                }

                controls.Add(ColourControlCapability.Supported(MapControl(type), min, max, step, def,
                    MapControl(type) == ColourControl.Temperature ? "K" : "level", "AMD ADL"));
            }

            return new ColourCapabilities(ProviderName, Vendor, displayId, controls,
                "Ranges are read from the installed AMD driver, not assumed.");
        }, cancellationToken);

    internal static ColourControl MapControl(int adlType) => adlType switch
    {
        AdlNative.ColorBrightness => ColourControl.Brightness,
        AdlNative.ColorContrast => ColourControl.Contrast,
        AdlNative.ColorSaturation => ColourControl.Saturation,
        AdlNative.ColorHue => ColourControl.Hue,
        _ => ColourControl.Temperature
    };

    public Task<ColourState> GetCurrentStateAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var target = FindTarget(displayId);
            var settings = new ColourSettings();
            if (target is null || _session is null) return new ColourState(displayId, settings, ProviderName);

            foreach (var type in AdlNative.AllColourTypes)
            {
                if (_session.GetColour(target.AdapterIndex, target.DisplayIndex, type, out var current,
                        out _, out _, out _, out _) == AdlNative.AdlOk)
                {
                    settings = settings.With(MapControl(type), current);
                }
            }

            return new ColourState(displayId, settings, ProviderName);
        }, cancellationToken);
}
