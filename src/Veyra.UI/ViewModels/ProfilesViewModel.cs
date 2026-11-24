using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class GameProfileRow
{
    public GameProfileRow(GameProfile profile, string presetName)
    {
        Profile = profile;
        PresetName = presetName;
    }

    public GameProfile Profile { get; }
    public string Name => Profile.Name;
    public string ExecutableName => Profile.ExecutableName;
    public string PresetName { get; }
    public bool HasPreset => !string.IsNullOrWhiteSpace(Profile.PresetId);
    public string Detail => HasPreset ? PresetName : "No look assigned";
}

public sealed class ProfilesViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private PresetCardViewModel? _selectedPreset;
    private string _gameName = string.Empty;
    private string _executableName = string.Empty;
    private string _status = "Pick a game, choose a look, and save the profile.";

    public override string Title => "Profiles";
    public override string Subtitle => "A look for each game — saved here, applied when you choose it.";
    public override string Glyph => "\uE77B";

    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public ObservableCollection<PresetCardViewModel> Presets { get; } = new();
    public ObservableCollection<GameProfileRow> Profiles { get; } = new();

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set => Set(ref _selectedDisplay, value);
    }

    public PresetCardViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set => Set(ref _selectedPreset, value);
    }

    public string GameName { get => _gameName; set => Set(ref _gameName, value); }
    public string ExecutableName { get => _executableName; set => Set(ref _executableName, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public Visibility EmptyVisibility => Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string ProfileCount => Profiles.Count == 1 ? "1 saved profile" : $"{Profiles.Count} saved profiles";

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(default);
            Displays.Clear();
            foreach (var display in displays) Displays.Add(display);
            SelectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();

            var presets = await AppServices.Presets.GetAllAsync(default);
            Presets.Clear();
            foreach (var preset in presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                Presets.Add(new PresetCardViewModel(preset));
            }

            SelectedPreset ??= Presets.FirstOrDefault(p => p.Id == "builtin.balanced") ?? Presets.FirstOrDefault();
            await ReloadProfilesAsync();
        }
        catch (Exception ex)
        {
            Status = "Profiles could not be loaded: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadProfilesAsync()
    {
        var profiles = await AppServices.Games.GetAllAsync(default);
        var presets = await AppServices.Presets.GetAllAsync(default);
        var presetNames = presets.ToDictionary(p => p.Id, p => p.Name, StringComparer.OrdinalIgnoreCase);

        Profiles.Clear();
        foreach (var profile in profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(new GameProfileRow(profile,
                profile.PresetId is not null && presetNames.TryGetValue(profile.PresetId, out var name)
                    ? name
                    : "Missing look"));
        }

        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ProfileCount));
    }

    public void PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a game executable",
            Filter = "Executables (*.exe)|*.exe",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        ExecutableName = Path.GetFileName(dialog.FileName);
        if (string.IsNullOrWhiteSpace(GameName))
        {
            GameName = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
        Status = $"{ExecutableName} selected. Choose a look and save.";
    }

    public async Task SaveProfileAsync()
    {
        var name = GameName.Trim();
        var exe = ExecutableName.Trim();

        if (name.Length == 0 || exe.Length == 0)
        {
            Status = "A game name and executable are both required.";
            return;
        }

        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Status = "The executable must end in .exe.";
            return;
        }

        if (SelectedPreset is null)
        {
            Status = "Choose a look to assign to this game.";
            return;
        }

        var existing = Profiles.FirstOrDefault(p =>
            string.Equals(p.ExecutableName, exe, StringComparison.OrdinalIgnoreCase));
        var profile = new GameProfile
        {
            Id = existing?.Profile.Id ?? "game." + Guid.NewGuid().ToString("N"),
            Name = name,
            ExecutableName = exe,
            PresetId = SelectedPreset.Id,
            AutoSwitch = true,
            Priority = existing?.Profile.Priority ?? 100,
            LastDetected = DateTimeOffset.UtcNow
        };

        await AppServices.Games.SaveAsync(profile, default);
        await ReloadProfilesAsync();
        GameName = string.Empty;
        ExecutableName = string.Empty;
        Status = $"{name} now uses {SelectedPreset.Name}. Profiles apply when you press Apply.";
    }

    public async Task ApplyAsync(GameProfileRow row)
    {
        if (SelectedDisplay is null)
        {
            Status = "No display is available.";
            return;
        }

        if (row.Profile.PresetId is null)
        {
            Status = $"{row.Name} has no look assigned.";
            return;
        }

        var preset = await AppServices.Presets.GetAsync(row.Profile.PresetId, default);
        if (preset is null)
        {
            Status = $"The look assigned to {row.Name} no longer exists.";
            return;
        }

        var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, preset.Colour, default);
        Status = result.IsSuccess ? $"{preset.Name} applied for {row.Name}." : result.Message;
    }

    public async Task DeleteAsync(GameProfileRow row)
    {
        await AppServices.Games.DeleteAsync(row.Profile.Id, default);
        await ReloadProfilesAsync();
        Status = $"{row.Name} was removed.";
    }
}
