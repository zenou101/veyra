namespace Veyra.Display.Monitors;

/// <summary>Resolves any stable display identifier Veyra stores back to a GDI device name.</summary>
public static class DisplayIds
{
    public static string? ResolveGdiName(string displayId)
    {
        if (string.IsNullOrWhiteSpace(displayId)) return null;

        // A GDI device name looks like "\.\DISPLAY1": two leading separators.
        if (displayId.Length > 3 && displayId[0] == (char)92 && displayId[1] == (char)92)
        {
            return displayId;
        }

        var display = DisplayService.EnumerateDisplays()
            .FirstOrDefault(d => DisplayService.Matches(d, displayId));
        return display?.GdiDeviceName;
    }
}
