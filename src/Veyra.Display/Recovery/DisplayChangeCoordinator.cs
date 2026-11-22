namespace Veyra.Display.Recovery;

/// <summary>
/// Serialises display changes across processes with a named mutex, so a second
/// operation cannot interleave with, or revert, work that is already in progress.
/// </summary>
public static class DisplayChangeCoordinator
{
    public static readonly string MutexName = "Local" + (char)92 + "Veyra.DisplayChange";

    public static bool TryEnter(TimeSpan timeout, out Mutex? mutex, out string error)
    {
        error = string.Empty;
        mutex = null;

        try
        {
            var candidate = new Mutex(false, MutexName);
            bool acquired;
            try
            {
                acquired = candidate.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                candidate.Dispose();
                error = "Another display change is already in progress. Wait for it to finish.";
                return false;
            }

            mutex = candidate;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            error = "The display change lock could not be created: " + ex.Message;
            return false;
        }
    }

    public static void Exit(Mutex? mutex)
    {
        if (mutex is null) return;
        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread; nothing to release.
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
