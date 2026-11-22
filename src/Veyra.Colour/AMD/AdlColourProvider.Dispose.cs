namespace Veyra.Colour.AMD;

public sealed partial class AdlColourProvider
{
    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
