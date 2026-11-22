using Veyra.Core;

namespace Veyra.Colour.AMD;

public sealed partial class AdlColourProvider
{
    /// <summary>
    /// Reads what the driver actually reports and, optionally, performs one small write
    /// per control followed by an immediate restore. Used by Veyra.ColourProbe and the
    /// diagnostics panel; never used to apply presets.
    /// </summary>
    public AdlDiagnostics Diagnose(bool performWriteTest)
    {
        var lines = new List<string>();
        var controls = new List<AdlControlReport>();

        if (!EnsureInitialised() || _session is null)
        {
            lines.Add("ADL unavailable: " + (_initError.Length > 0 ? _initError : "no session"));
            return new AdlDiagnostics(false, _initError, lines, controls);
        }

        lines.Add("ADL context opened.");
        foreach (var target in _targets)
        {
            lines.Add($"adapter {target.AdapterIndex} display {target.DisplayIndex} -> {target.GdiName} (caps 0x{target.Caps:X}, valids 0x{target.Valids:X})");

            foreach (var type in AdlNative.AllColourTypes)
            {
                var status = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                    out var current, out var def, out var min, out var max, out var step);

                var supported = status == AdlNative.AdlOk && max > min;
                controls.Add(new AdlControlReport(type, AdlNative.DescribeColourType(type), supported,
                    current, def, min, max, step, status));
                lines.Add($"  {AdlNative.DescribeColourType(type)}: status={status} supported={supported} value={current} default={def} min={min} max={max} step={step}");
            }
        }

        if (performWriteTest)
        {
            foreach (var target in _targets)
            {
                lines.Add(RunWriteTests(target));
            }
        }

        return new AdlDiagnostics(true, string.Empty, lines, controls);
    }

    /// <summary>Raw adapter/display mapping, used when no colour target can be mapped.</summary>
    public IReadOnlyList<string> ExplainMapping()
    {
        var lines = new List<string>();
        EnsureInitialised();
        if (_session is null)
        {
            lines.Add("no ADL session: " + _initError);
            return lines;
        }

        var adapters = _session.GetAdapters(out var adapterError);
        lines.Add($"adapters={adapters.Count} error='{adapterError}'");
        foreach (var adapter in adapters)
        {
            lines.Add($"adapter {adapter.AdapterIndex} present={adapter.Present} exist={adapter.Exist} gdi='{adapter.DisplayName}' os={adapter.OsDisplayIndex} name='{adapter.AdapterName}' size={adapter.Size}");
        }

        foreach (var adapter in adapters)
        {
            var displays = _session.GetDisplays(adapter.AdapterIndex, adapter.AdapterName, out var displayError);
            lines.Add($"displays for adapter {adapter.AdapterIndex}: {displays.Count} error='{displayError}'");
            foreach (var display in displays)
            {
                var colourStatus = _session.GetColour(display.AdapterIndex, display.DisplayIndex, AdlNative.ColorSaturation,
                    out var current, out _, out var min, out var max, out var step);
                var capsStatus = _session.GetColourCaps(display.AdapterIndex, display.DisplayIndex, out var caps, out var valids);
                lines.Add($"  display={display.DisplayIndex} logicalAdapter={display.LogicalAdapterIndex} mask=0x{display.InfoMask:X} value=0x{display.InfoValue:X} name='{display.DisplayName}' colour={colourStatus} {current}/{min}..{max} step={step} caps={capsStatus} 0x{caps:X}/0x{valids:X}");
            }
        }

        return lines;
    }

    private string RunWriteTests(AdlTargetMapping target)
    {
        if (_session is null) return "write test: no session";

        var results = new List<string>();
        foreach (var type in AdlNative.AllColourTypes)
        {
            var read = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                out var current, out _, out var min, out var max, out var step);
            if (read != AdlNative.AdlOk || max <= min)
            {
                results.Add($"{AdlNative.DescribeColourType(type)}=unavailable({read})");
                continue;
            }

            var probe = Math.Clamp(current + Math.Max(step, (max - min) / 20), min, max);
            var write = _session.SetColour(target.AdapterIndex, target.DisplayIndex, type, probe);
            var verify = _session.GetColour(target.AdapterIndex, target.DisplayIndex, type,
                out var after, out _, out _, out _, out _);
            var restore = _session.SetColour(target.AdapterIndex, target.DisplayIndex, type, current);

            var accepted = write == AdlNative.AdlOk && verify == AdlNative.AdlOk && after == probe;
            results.Add($"{AdlNative.DescribeColourType(type)}: set {current}->{probe} accepted={accepted} (write {write}, read {after}, restore {restore})");
        }

        return $"write tests for {target.GdiName}: " + string.Join(" | ", results);
    }
}
