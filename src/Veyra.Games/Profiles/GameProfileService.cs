using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.Colour;
using Veyra.Games.Detection;

namespace Veyra.Games.Profiles;

public sealed record ProfileActivationNotice(string Title, string Detail, bool IsWarning);

/// <summary>
/// Applies a game's preset when it starts and restores the previous colour state when the
/// last configured game exits. Display-mode changes are never performed automatically.
/// </summary>
public sealed class GameProfileService : IDisposable
{
    private readonly IGameRepository _games;
    private readonly IPresetRepository _presets;
    private readonly ColourEngine _colour;
    private readonly IDisplayService _displays;
    private ProcessWatcher? _watcher;
    private GameProfile? _active;
    private string? _activeDisplayId;
    private bool _busy;

    public GameProfileService(IGameRepository games, IPresetRepository presets, ColourEngine colour, IDisplayService displays)
    {
        _games = games;
        _presets = presets;
        _colour = colour;
        _displays = displays;
    }

    public event EventHandler<ProfileActivationNotice>? Notified;

    public GameProfile? ActiveProfile => _active;

    public async Task StartAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.AutomaticGameProfiles) return;

        var profiles = await _games.GetAllAsync(cancellationToken);
        _watcher?.Dispose();
        _watcher = new ProcessWatcher(profiles.Select(p => p.ExecutableName));
        _watcher.ProcessChanged += async (_, args) => await EvaluateAsync(args.ProcessName, args.Started, cancellationToken);
        _watcher.Start(TimeSpan.FromMilliseconds(settings.MonitoringIntervalMs));
    }

    public void Stop() => _watcher?.Dispose();

    private async Task EvaluateAsync(string processName, bool started, CancellationToken cancellationToken)
    {
        if (_busy) return;
        _busy = true;

        try
        {
            var profiles = await _games.GetAllAsync(cancellationToken);
            var running = (_watcher?.CurrentProcessNames ?? Array.Empty<string>()).ToList();
            if (started && !running.Contains(processName, StringComparer.OrdinalIgnoreCase)) running.Add(processName);

            var selection = ProfileResolver.ResolveForProcesses(profiles, running);

            if (selection.Active is null)
            {
                if (_active is not null)
                {
                    await RestoreAsync(cancellationToken);
                    Notified?.Invoke(this, new ProfileActivationNotice("Desktop profile restored",
                        $"{_active.Name} closed; your previous colour settings are back.", false));
                    _active = null;
                }

                return;
            }

            if (_active?.Id == selection.Active.Id) return;

            if (_active is not null) await RestoreAsync(cancellationToken);

            var notice = await ActivateAsync(selection.Active, cancellationToken);
            if (notice is not null) Notified?.Invoke(this, notice);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Notified?.Invoke(this, new ProfileActivationNotice("Profile switch failed", ex.Message, true));
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<ProfileActivationNotice?> ActivateAsync(GameProfile profile, CancellationToken cancellationToken)
    {
        if (!profile.AutoSwitch) return null;

        if (profile.PresetId is null)
        {
            return new ProfileActivationNotice("No preset assigned",
                $"{profile.Name} started, but it has no preset to apply.", true);
        }

        var preset = await _presets.GetAsync(profile.PresetId, cancellationToken);
        if (preset is null)
        {
            return new ProfileActivationNotice("Preset missing",
                $"The preset assigned to {profile.Name} no longer exists.", true);
        }

        var displays = await _displays.GetDisplaysAsync(cancellationToken);
        var display = displays.FirstOrDefault(d => d.Id == preset.MonitorId) ?? displays.FirstOrDefault(d => d.IsPrimary);
        if (display is null)
        {
            return new ProfileActivationNotice("No display", "No connected display was available to apply the preset.", true);
        }

        var result = await _colour.ApplyAsync(display, preset.Colour, cancellationToken);
        _active = profile;
        _activeDisplayId = display.Id;

        return result.IsSuccess
            ? new ProfileActivationNotice("Profile applied", $"{preset.Name} applied for {profile.Name}.", false)
            : new ProfileActivationNotice("Profile failed", result.Message, true);
    }

    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (_activeDisplayId is null) return;

        var displays = await _displays.GetDisplaysAsync(cancellationToken);
        var display = displays.FirstOrDefault(d => d.Id == _activeDisplayId);
        if (display is not null)
        {
            await _colour.RestoreAsync(display, cancellationToken);
        }

        _activeDisplayId = null;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
