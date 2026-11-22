using System.Diagnostics;
using Veyra.Core.Models;
using Veyra.Display.Monitors;
using Veyra.Display.Recovery;
using Veyra.Storage;

namespace Veyra.DisplayGuard;

/// <summary>
/// Out-of-process recovery guard. It is started before a display change and reverts the
/// exact captured configuration unless the main process confirms, dies or stops
/// responding within its own timeout. It never invents a replacement mode.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var options = Parse(args);

        if (options is null)
        {
            Log("Guard started without the required arguments; nothing to do.");
            return 2;
        }

        try
        {
            return Run(options).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log("Guard failed unexpectedly: " + ex);
            return 5;
        }
    }

    private static async Task<int> Run(GuardOptions options)
    {
        using var confirm = TryOpenConfirm(options.ConfirmEvent);
        if (confirm is null)
        {
            Log("Confirmation event was not available; reverting immediately.");
            return Revert(options.SnapshotPath);
        }

        var confirmed = Task.Run(() => confirm.WaitOne());
        Task processExit;

        try
        {
            using var process = Process.GetProcessById(options.ProcessId);
            processExit = process.WaitForExitAsync();
        }
        catch (ArgumentException)
        {
            Log("The main process had already exited; reverting.");
            return Revert(options.SnapshotPath);
        }

        var timeout = Task.Delay(options.TimeoutMs);
        var winner = await Task.WhenAny(confirmed, processExit, timeout);

        if (winner == confirmed && confirm.WaitOne(0))
        {
            Log("Change confirmed by the main process; the display is left as it is.");
            Cleanup(options.SnapshotPath);
            return 0;
        }

        var reason = winner == timeout ? "confirmation timed out" : "the main process exited";
        Log($"Reverting because {reason}.");

        return Revert(options.SnapshotPath);
    }

    private static int Revert(string snapshotPath)
    {
        if (!DisplaySnapshotStore.TryLoad(snapshotPath, out var snapshot, out var error) || snapshot is null)
        {
            Log("Recovery refused: " + error);
            return 3;
        }

        if (!DisplayChangeCoordinator.TryEnter(TimeSpan.FromSeconds(3), out var mutex, out var mutexError))
        {
            Log("Recovery could not take the display-change lock: " + mutexError);
            return 4;
        }

        try
        {
            var failures = new List<string>();
            foreach (var state in snapshot.Displays)
            {
                var result = DisplayModeApplier.Restore(state);
                Log(result.IsSuccess
                    ? $"Restored {state.GdiDeviceName} to {state.Width}x{state.Height}@{state.RefreshRate}."
                    : $"Could not restore {state.GdiDeviceName}: {result.Message}");

                if (!result.IsSuccess) failures.Add(result.Message);
            }

            if (failures.Count > 0) return 6;

            Cleanup(snapshotPath);
            return 0;
        }
        finally
        {
            DisplayChangeCoordinator.Exit(mutex);
        }
    }

    private static void Cleanup(string snapshotPath)
    {
        try
        {
            if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
        }
        catch (IOException ex)
        {
            Log("Snapshot could not be removed: " + ex.Message);
        }
    }

    private static EventWaitHandle? TryOpenConfirm(string name)
    {
        try
        {
            return EventWaitHandle.OpenExisting(name);
        }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static GuardOptions? Parse(string[] args)
    {
        string? snapshot = null, confirm = null;
        var pid = 0;
        var timeout = 15000;

        for (var i = 0; i < args.Length - 1; i += 2)
        {
            switch (args[i])
            {
                case "--snapshot": snapshot = args[i + 1]; break;
                case "--pid": int.TryParse(args[i + 1], out pid); break;
                case "--confirm": confirm = args[i + 1]; break;
                case "--timeout": int.TryParse(args[i + 1], out timeout); break;
            }
        }

        if (string.IsNullOrWhiteSpace(snapshot) || string.IsNullOrWhiteSpace(confirm) || pid <= 0) return null;

        return new GuardOptions(snapshot, pid, confirm, Math.Clamp(timeout, 3000, 120000));
    }

    private static void Log(string message)
    {
        try
        {
            var logs = Path.Combine(VeyraPaths.Root, "logs");
            Directory.CreateDirectory(logs);
            File.AppendAllText(
                Path.Combine(logs, "display-guard.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The guard must keep working even when logging is impossible.
        }
    }

    private sealed record GuardOptions(string SnapshotPath, int ProcessId, string ConfirmEvent, int TimeoutMs);
}
