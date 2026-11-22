namespace Veyra.Colour.AMD;

public sealed partial class AdlColourProvider
{
    private bool? _writeSupported;
    private string _writeDetail = "Write support has not been checked yet.";

    /// <summary>
    /// ADL can report a control as valid and still reject every write (AMD gates colour
    /// writes on some driver builds). This performs one small saturation change and
    /// restores it, so the UI never offers a control the driver refuses.
    /// </summary>
    public bool WritesSupported
    {
        get
        {
            if (_writeSupported is null) VerifyWriteSupport();
            return _writeSupported == true;
        }
    }

    public string WriteSupportDetail => _writeDetail;

    public bool VerifyWriteSupport()
    {
        if (_writeSupported.HasValue) return _writeSupported.Value;

        if (!EnsureInitialised() || _session is null || _targets.Count == 0)
        {
            _writeSupported = false;
            _writeDetail = "ADL is not available, so driver colour writes cannot be verified.";
            return false;
        }

        var target = _targets[0];
        var read = _session.GetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation,
            out var current, out _, out var min, out var max, out var step);

        if (read != AdlNative.AdlOk || max <= min)
        {
            _writeSupported = false;
            _writeDetail = $"The AMD driver does not expose a saturation control (ADL status {read}).";
            return false;
        }

        var probe = Math.Clamp(current + Math.Max(step, 1), min, max);
        var write = _session.SetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation, probe);
        var verify = _session.GetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation,
            out var after, out _, out _, out _, out _);
        _session.SetColour(target.AdapterIndex, target.DisplayIndex, AdlNative.ColorSaturation, current);

        _writeSupported = write == AdlNative.AdlOk && verify == AdlNative.AdlOk && after == probe;
        _writeDetail = _writeSupported.Value
            ? "The driver accepted a test colour write and it was restored."
            : $"The installed AMD driver rejects ADL colour writes (status {write}); AMD software may need to enable custom colour first. Veyra will not offer a control that cannot change the display.";
        return _writeSupported.Value;
    }
}
