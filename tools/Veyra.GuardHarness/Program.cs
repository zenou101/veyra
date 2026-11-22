using Veyra.Core.Models;
using Veyra.Display.Monitors;
using Veyra.Display.Recovery;

// Guard rollback harness: applies one refresh-rate change for real, refuses to confirm it,
// and checks that the separate guard process restores the previous mode on its own timeout.

var refresh = 60;
var timeoutSeconds = 25;
string? displayFilter = null;

for (var i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--refresh": refresh = int.Parse(args[i + 1]); break;
        case "--timeout": timeoutSeconds = int.Parse(args[i + 1]); break;
        case "--display": displayFilter = args[i + 1]; break;
    }
}

var service = new DisplayService();
var displays = await service.GetDisplaysAsync(CancellationToken.None);
var display = displayFilter is null
    ? displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault()
    : displays.FirstOrDefault(d => d.Id.Contains(displayFilter, StringComparison.OrdinalIgnoreCase)) ?? displays.FirstOrDefault(d => d.FriendlyName.Contains(displayFilter, StringComparison.OrdinalIgnoreCase));

if (display is null)
{
    Console.WriteLine("FAIL: no display was found.");
    return 2;
}

Console.WriteLine($"display: {display.FriendlyName} ({display.Id})");
Console.WriteLine($"before:  {display.ResolutionLabel} {display.RefreshLabel}");

var state = await service.CaptureStateAsync(display.Id, CancellationToken.None);
if (state is null)
{
    Console.WriteLine("FAIL: the current display state could not be captured, so nothing was changed.");
    return 2;
}

var modes = await service.GetModesAsync(display.Id, CancellationToken.None);
var mode = modes
    .Where(m => m.Width == display.Width && m.Height == display.Height && m.RefreshRate == refresh)
    .OrderBy(m => m.BitsPerPixel)
    .FirstOrDefault();

if (mode is null)
{
    Console.WriteLine($"FAIL: {display.Width}x{display.Height}@{refresh} is not in the driver's mode list.");
    return 2;
}

using var guard = DisplayGuardSession.Start(new[] { state }, timeoutSeconds, out var guardError);
if (guard is null)
{
    Console.WriteLine("FAIL: " + guardError);
    return 2;
}

Console.WriteLine($"guard:   started for pid {Environment.ProcessId}, timeout {timeoutSeconds}s, token {guard.Token}");
Console.WriteLine($"apply:   {mode.Label}");

var apply = await service.ApplyModeAsync(display, mode, CancellationToken.None);
Console.WriteLine($"apply:   {apply}");

var applied = (await service.GetDisplaysAsync(CancellationToken.None)).FirstOrDefault(d => d.Id == display.Id);
Console.WriteLine($"after apply: {applied?.RefreshLabel ?? "unknown"}");

if (!apply.IsSuccess)
{
    guard.Confirm();
    return 3;
}

Console.WriteLine($"waiting: {timeoutSeconds + 8}s without confirming, so the guard's own timeout must revert the change…");
await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds + 8));

var restored = (await service.GetDisplaysAsync(CancellationToken.None)).FirstOrDefault(d => d.Id == display.Id);
Console.WriteLine($"guard running: {guard.GuardRunning}");
Console.WriteLine($"after guard:   {restored?.RefreshLabel ?? "unknown"}");

var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veyra", "logs", "display-guard.log");
if (File.Exists(logPath))
{
    Console.WriteLine("guard log (tail):");
    foreach (var line in File.ReadLines(logPath).TakeLast(8)) Console.WriteLine("  " + line);
}

guard.Confirm();

var passed = restored is not null && restored.RefreshRate == state.RefreshRate && restored.Width == state.Width && restored.Height == state.Height;
Console.WriteLine(passed ? "RESULT rollback-verified=True" : "RESULT rollback-verified=False");
return passed ? 0 : 3;
