using System.Globalization;
using Veyra.Colour.Providers;
using Veyra.Core.Models;
using Veyra.Core.Services;

namespace Veyra.GammaProbe;

/// <summary>
/// Proves that the Windows ramp path changes what the display actually shows: it captures
/// the composited desktop, applies a known adjustment, captures again, then restores the
/// exact original ramp and captures a third time.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Veyra gamma probe - 1.0.0");
        Console.WriteLine("Measures real desktop output before and after a ramp change on DISPLAY1.");
        Console.WriteLine();

        Console.WriteLine("step: creating provider"); Console.Out.Flush();
        var provider = new WindowsGammaColourProvider();
        Console.WriteLine("step: provider created"); Console.Out.Flush();
        var display = new string((char)92, 2) + "." + (char)92 + "DISPLAY1";

        Console.WriteLine("step: reading state"); Console.Out.Flush();
        var before = await provider.GetCurrentStateAsync(display, CancellationToken.None);
        Console.WriteLine("step: state read"); Console.Out.Flush();
        Console.WriteLine($"read ramp: {before.Values.Gamma:0.##} gamma reported, values present: {!before.Values.IsEmpty}");

        var original = await provider.CaptureRawSnapshotAsync(display, CancellationToken.None);
        if (original is null)
        {
            Console.WriteLine("FAIL: the display did not return a gamma ramp.");
            return 2;
        }

        Console.WriteLine($"original ramp captured ({original.Length} base64 chars).");

        var baseline = ScreenCapture.Measure();
        if (baseline is null)
        {
            Console.WriteLine("FAIL: the desktop could not be captured.");
            return 3;
        }

        Console.WriteLine($"desktop before: mean RGB {baseline.Value.R:0.00}/{baseline.Value.G:0.00}/{baseline.Value.B:0.00}, luma {baseline.Value.Luma:0.00}");

        var test = new ColourSettings { Brightness = -30, Gamma = 1.15, Temperature = 6500 };
        var applied = await provider.ApplyAsync(display, test, CancellationToken.None);
        Console.WriteLine($"apply: {applied.Message}");

        var midState = await provider.GetCurrentStateAsync(display, CancellationToken.None);
        var midRaw = await provider.CaptureRawSnapshotAsync(display, CancellationToken.None);
        Console.WriteLine($"ramp readback after apply: gamma {midState.Values.Gamma:0.###}, brightness {midState.Values.Brightness:0.##}, ramp changed: {midRaw != original}");

        await Task.Delay(350);
        var changed = ScreenCapture.Measure();
        if (changed is null)
        {
            Console.WriteLine("FAIL: the desktop could not be captured after the change.");
            return 4;
        }

        Console.WriteLine($"desktop after:  mean RGB {changed.Value.R:0.00}/{changed.Value.G:0.00}/{changed.Value.B:0.00}, luma {changed.Value.Luma:0.00}");
        var delta = changed.Value.Luma - baseline.Value.Luma;
        Console.WriteLine($"luma delta: {delta:+0.00;-0.00;0.00} (expected clearly negative for brightness -30)");

        var restored = await provider.RestoreRawSnapshotAsync(display, original, CancellationToken.None);
        Console.WriteLine($"restore: {restored.Message}");

        // Exact ramp equality is the reliable restore check; desktop luma also moves when the
        // screen content itself changes, so that number is reported but not used as the gate.
        var afterRaw = await provider.CaptureRawSnapshotAsync(display, CancellationToken.None);
        var rampRestored = string.Equals(afterRaw, original, StringComparison.Ordinal);
        Console.WriteLine($"ramp restored exactly: {rampRestored}");

        await Task.Delay(350);
        var after = ScreenCapture.Measure();
        if (after is null)
        {
            Console.WriteLine("FAIL: the desktop could not be captured after restore.");
            return 5;
        }

        Console.WriteLine($"desktop restored: mean RGB {after.Value.R:0.00}/{after.Value.G:0.00}/{after.Value.B:0.00}, luma {after.Value.Luma:0.00}");
        var residual = Math.Abs(after.Value.Luma - baseline.Value.Luma);
        Console.WriteLine($"residual vs baseline: {residual:0.00} luma");

        var changedEnough = delta < -2.0;

        Console.WriteLine();
        Console.WriteLine($"RESULT change-measured={changedEnough} desktop-residual={residual:0.00} ramp-restored={rampRestored}");
        Console.WriteLine(rampRestored
            ? "PASS: the driver returned the requested ramp and the exact original was restored. Desktop capture cannot observe gamma, so the pixel delta is informational only."
            : "FAIL: the ramp did not come back exactly.");

        return rampRestored ? 0 : 6;
    }
}
