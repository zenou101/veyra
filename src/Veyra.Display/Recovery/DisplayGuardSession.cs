using System.Diagnostics;
using Veyra.Core.Models;
using Veyra.Storage;

namespace Veyra.Display.Recovery;

/// <summary>
/// Owns an out-of-process guard for one display change. The guard reverts the exact
/// previous configuration unless this process explicitly confirms, so recovery does
/// not depend on the UI thread staying alive.
/// </summary>
public sealed class DisplayGuardSession : IDisposable
{
    private readonly EventWaitHandle _confirm;
    private readonly Process? _process;

    private DisplayGuardSession(EventWaitHandle confirm, Process? process, string token, string snapshotPath)
    {
        _confirm = confirm;
        _process = process;
        Token = token;
        SnapshotPath = snapshotPath;
    }

    public string Token { get; }
    public string SnapshotPath { get; }
    public bool GuardRunning => _process is { HasExited: false };

    public static DisplayGuardSession? Start(IReadOnlyList<DisplayState> states, int timeoutSeconds, out string error)
    {
        error = string.Empty;

        if (states.Count == 0)
        {
            error = "No display state was captured, so the change was not attempted.";
            return null;
        }

        var guardPath = Path.Combine(AppContext.BaseDirectory, "Veyra.DisplayGuard.exe");
        if (!File.Exists(guardPath))
        {
            error = "The recovery guard (Veyra.DisplayGuard.exe) is missing, so the change was blocked.";
            return null;
        }

        var token = Guid.NewGuid().ToString("N");
        var snapshotPath = Path.Combine(VeyraPaths.Root, "display-guard-" + token + ".json");

        try
        {
            DisplaySnapshotStore.Save(snapshotPath, new DisplayRecoverySnapshot { Token = token, Displays = states });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "The recovery snapshot could not be written: " + ex.Message;
            return null;
        }

        var confirmName = "Local" + (char)92 + "Veyra.Confirm." + token;
        EventWaitHandle confirm;
        try
        {
            confirm = new EventWaitHandle(false, EventResetMode.ManualReset, confirmName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "The confirmation event could not be created: " + ex.Message;
            return null;
        }

        var startInfo = new ProcessStartInfo(guardPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--snapshot");
        startInfo.ArgumentList.Add(snapshotPath);
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--confirm");
        startInfo.ArgumentList.Add(confirmName);
        startInfo.ArgumentList.Add("--timeout");
        startInfo.ArgumentList.Add((Math.Clamp(timeoutSeconds, 5, 60) * 1000).ToString());

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                confirm.Dispose();
                error = "The recovery guard could not be started.";
                return null;
            }

            return new DisplayGuardSession(confirm, process, token, snapshotPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            confirm.Dispose();
            error = "The recovery guard could not be started: " + ex.Message;
            return null;
        }
    }

    /// <summary>Keeps the new configuration. The guard exits without touching the display.</summary>
    public void Confirm() => _confirm.Set();

    public void Dispose() => _confirm.Dispose();
}
