using Veyra.Core.Models;
using Veyra.Display.Interop;

namespace Veyra.Display.Monitors;

public sealed partial class DisplayService
{
    public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<DisplayMode>>(() =>
        {
            var displays = EnumerateDisplays();
            var display = displays.FirstOrDefault(d => Matches(d, displayId)) ?? displays.FirstOrDefault();
            if (display is null) return Array.Empty<DisplayMode>();

            var modes = new HashSet<(int Width, int Height, int Refresh, int Bits, int Orientation)>();
            for (var i = 0; i < 4000; i++)
            {
                var devMode = DisplayNative.CreateDevMode();
                if (!DisplayNative.EnumDisplaySettingsEx(display.GdiDeviceName, i, ref devMode, DisplayNative.EdsRawMode)) break;

                if (devMode.PelsWidth < 320 || devMode.PelsHeight < 200) continue;
                if (devMode.DisplayFrequency is < 20 or > 500) continue;
                if (devMode.BitsPerPel is not (16 or 24 or 32)) continue;

                modes.Add(((int)devMode.PelsWidth, (int)devMode.PelsHeight, (int)devMode.DisplayFrequency,
                           (int)devMode.BitsPerPel, (int)devMode.DisplayOrientation));
            }

            return modes
                .Select(m => new DisplayMode(m.Width, m.Height, m.Refresh, m.Bits, m.Orientation))
                .OrderByDescending(m => m.Width * m.Height)
                .ThenByDescending(m => m.RefreshRate)
                .ToArray();
        }, cancellationToken);

    public Task<DisplayState?> CaptureStateAsync(string displayId, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var display = EnumerateDisplays().FirstOrDefault(d => Matches(d, displayId));
            if (display is null) return null;

            var devMode = DisplayNative.CreateDevMode();
            if (!DisplayNative.EnumDisplaySettingsEx(display.GdiDeviceName, DisplayNative.EnumCurrentSettings, ref devMode, 0))
            {
                return null;
            }

            return new DisplayState(
                display.Id,
                display.GdiDeviceName,
                (int)devMode.PelsWidth,
                (int)devMode.PelsHeight,
                (int)devMode.DisplayFrequency,
                (int)devMode.BitsPerPel,
                (int)devMode.DisplayOrientation,
                devMode.PositionX,
                devMode.PositionY,
                (int)devMode.DisplayFixedOutput,
                (int)devMode.DisplayFlags);
        }, cancellationToken);
}
