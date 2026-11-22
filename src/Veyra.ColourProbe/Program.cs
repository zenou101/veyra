using Veyra.Colour.AMD;
using Veyra.Colour.NVIDIA;
using Veyra.Colour.Providers;


Console.WriteLine("Veyra colour probe — 1.0.0");

if (args.Contains("--nvidia"))
{
    using var nvidia = new NvidiaSaturationService();
    var available = await nvidia.InitializeAsync();
    Console.WriteLine("NVIDIA available: " + available);
    Console.WriteLine(nvidia.Detail);
    return available ? 0 : 2;
}

if (args.Contains("--read"))
{
    using var reader = new AdlColourProvider();
    var connected = await new Veyra.Display.Monitors.DisplayService().GetDisplaysAsync(CancellationToken.None);
    foreach (var display in connected)
    {
        var state = await reader.GetCurrentStateAsync(display.GdiDeviceName, CancellationToken.None);
        Console.WriteLine($"READ {display.GdiDeviceName} saturation={state.Values.Saturation?.ToString() ?? "unavailable"}");
    }
    return 0;
}

Console.WriteLine("Reads the installed AMD driver's real colour capabilities, performs one small write test and restores it.");
Console.WriteLine();

using var adl = new AdlColourProvider();
var diagnostics = adl.Diagnose(performWriteTest: true);

Console.WriteLine("== ADL ==");
Console.WriteLine("loaded: " + diagnostics.Loaded);
if (!string.IsNullOrEmpty(diagnostics.Detail)) Console.WriteLine("detail: " + diagnostics.Detail);
foreach (var line in diagnostics.Lines) Console.WriteLine(line);
foreach (var line in adl.ExplainMapping()) Console.WriteLine("MAP " + line);

Console.WriteLine();
Console.WriteLine("== Windows gamma ramp ==");
var gamma = new WindowsGammaColourProvider();
var displays = await new Veyra.Display.Monitors.DisplayService().GetDisplaysAsync(CancellationToken.None);
foreach (var display in displays)
{
    var caps = await gamma.GetCapabilitiesAsync(display.GdiDeviceName, CancellationToken.None);
    var ramp = await gamma.CaptureRawSnapshotAsync(display.GdiDeviceName, CancellationToken.None);
    var state = await gamma.GetCurrentStateAsync(display.GdiDeviceName, CancellationToken.None);
    Console.WriteLine($"{display.GdiDeviceName} | {display.FriendlyName} | {display.Width}x{display.Height}@{display.RefreshRate} | ramp={(ramp is null ? "unavailable" : ramp.Length + " chars")}");
    Console.WriteLine($"  current estimate: {state.Values}");
    Console.WriteLine($"  supported: {string.Join(", ", caps.Controls.Where(c => c.IsSupported).Select(c => c.Control))}");
}

Console.WriteLine();
Console.WriteLine("Probe complete. No permanent change was requested; the write test restored the original value.");
return diagnostics.Loaded ? 0 : 2;
