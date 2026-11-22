using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Veyra.Games.Detection;
using Xunit;

namespace Veyra.Games.Tests;

public sealed class ProcessWatcherTests
{
    [Fact]
    public void Watcher_only_tracks_configured_executables()
    {
        using var watcher = new ProcessWatcher(new[] { "definitely-not-running-veyra.exe" });
        watcher.Start(TimeSpan.FromMilliseconds(200));
        System.Threading.Thread.Sleep(400);
        Assert.Empty(watcher.CurrentProcessNames);
    }

    [Fact]
    public async Task Watcher_reports_notepad_starting_and_stopping()
    {
        using var watcher = new ProcessWatcher(new[] { "notepad" });
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        watcher.ProcessChanged += (_, args) =>
        {
            if (!args.ProcessName.Contains("notepad", StringComparison.OrdinalIgnoreCase)) return;
            if (args.Started) started.TrySetResult(true); else stopped.TrySetResult(true);
        };

        watcher.Start(TimeSpan.FromMilliseconds(150));

        using var process = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        Assert.NotNull(process);
        Assert.NotEqual(0, process!.Id);

        var startResult = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(6)));
        Assert.True(startResult == started.Task, "The watcher did not report Notepad starting.");

        process.Kill();
        var stopResult = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(6)));
        Assert.True(stopResult == stopped.Task, "The watcher did not report Notepad stopping.");
    }
}
