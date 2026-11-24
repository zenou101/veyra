using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class LibraryViewModel : PageViewModel
{
    private DisplayInfo? _selectedDisplay;
    private string _status = "Choose a look to apply it, or save the current sliders as a new one.";
    private string _newPresetName = string.Empty;

    public override string Title => "Library";
    public override string Subtitle => "Every look in one place. Built-ins stay read-only; your saved looks are yours to delete.";
    public override string Glyph => "\uE8F1";

    public ObservableCollection<PresetCardViewModel> Presets { get; } = new();
    public ObservableCollection<DisplayInfo> Displays { get; } = new();
    public string NewPresetName { get => _newPresetName; set => Set(ref _newPresetName, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set => Set(ref _selectedDisplay, value);
    }

    public string PresetCount => Presets.Count == 1 ? "1 look" : $"{Presets.Count} looks";

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(default);
            Displays.Clear();
            foreach (var display in displays) Displays.Add(display);
            SelectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Status = "The library could not be loaded: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadAsync()
    {
        var presets = await AppServices.Presets.GetAllAsync(default);
        Presets.Clear();
        foreach (var preset in presets.OrderBy(p => p.IsBuiltIn ? 0 : 1).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Presets.Add(new PresetCardViewModel(preset));
        }

        OnPropertyChanged(nameof(PresetCount));
    }

    public async Task ApplyAsync(PresetCardViewModel card)
    {
        if (SelectedDisplay is null)
        {
            Status = "No display is available to apply this look to.";
            return;
        }

        var result = await AppServices.Colour.ApplyAsync(SelectedDisplay, card.Preset.Colour, default);
        Status = result.IsSuccess ? $"{card.Name} applied to {SelectedDisplay.FriendlyName}." : result.Message;
    }

    public async Task DeleteAsync(PresetCardViewModel card)
    {
        if (card.IsBuiltIn)
        {
            Status = "Built-in looks cannot be deleted.";
            return;
        }

        await AppServices.Presets.DeleteAsync(card.Id, default);
        await ReloadAsync();
        Status = $"{card.Name} was removed from the library.";
    }

    public async Task SaveCurrentAsync()
    {
        if (SelectedDisplay is null)
        {
            Status = "No display is available to read the current look from.";
            return;
        }

        var name = NewPresetName.Trim();
        if (name.Length == 0)
        {
            Status = "Give the look a name before saving.";
            return;
        }

        if (name.Length > 40) name = name[..40];

        var state = await AppServices.Colour.GetCurrentStateAsync(SelectedDisplay, default);
        var preset = new Preset
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            Name = name,
            Description = "Saved from the Library page.",
            Kind = PresetKind.Colour,
            Colour = state.Values,
            MonitorId = SelectedDisplay.Id
        };

        await AppServices.Presets.SaveAsync(preset, default);
        await ReloadAsync();
        NewPresetName = string.Empty;
        Status = $"{preset.Name} saved.";
    }
}
