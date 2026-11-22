using System.Diagnostics;
using Veyra.Core;
using Veyra.Core.Services;

namespace Veyra.Games.Detection;

/// <summary>
/// Watches running processes on a configurable interval. It filters to configured game
/// names, raises start/stop events and never scans process paths or unrelated user data.
/// </summary>
public sealed class ProcessWatcher : IGameDetector
{
    private readonly object _sync = new();
    private readonly HashSet<string> _configured;
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private int _polling;

    public ProcessWatcher(IEnumerable<string> configuredExecutables)
    {
        _configured = configuredExecutables
            .Select(GameMatcher.NormaliseName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler<GameProcessChangedEventArgs>? ProcessChanged;

    public IReadOnlyCollection<string> CurrentProcessNames
    {
        get
        {
            lock (_sync) return _running.ToArray();
        }
    }

    public void Start(TimeSpan interval)
    {
        _timer?.Dispose();
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;

        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    var name = GameMatcher.NormaliseName(process.ProcessName);
                    if (_configured.Contains(name)) seen.Add(name);
                }
            }

            List<string> started, stopped;
            lock (_sync)
            {
                started = seen.Where(n => !_running.Contains(n)).ToList();
                stopped = _running.Where(n => !seen.Contains(n)).ToList();

                foreach (var name in stopped) _running.Remove(name);
                foreach (var name in started) _running.Add(name);
            }

            foreach (var name in started) ProcessChanged?.Invoke(this, new GameProcessChangedEventArgs(name, true));
            foreach (var name in stopped) ProcessChanged?.Invoke(this, new GameProcessChangedEventArgs(name, false));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A process disappearing mid-enumeration is normal; the next poll recovers.
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
