using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class HotkeyRow : ObservableObject
{
    private string _text;
    private bool _enabled;
    private string _error = string.Empty;

    public HotkeyRow(HotkeyBinding binding)
    {
        Binding = binding;
        _text = HotkeyCatalog.Format(binding.Spec);
        _enabled = binding.Enabled;
    }

    public HotkeyBinding Binding { get; }
    public string ActionLabel => Binding.ActionLabel;
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string Error { get => _error; set => Set(ref _error, value); }
    public bool HasError => !string.IsNullOrEmpty(Error);
}

public sealed class HotkeysViewModel : PageViewModel
{
    private string _status = "Hotkeys are registered with Windows while Veyra runs.";
    public override string Title => "Hotkeys";
    public override string Subtitle => "Global shortcuts that never require a background hook. Conflicts are reported, not hidden.";
    public override string Glyph => "\uE765";

    public ObservableCollection<HotkeyRow> Rows { get; } = new();
    public string Status { get => _status; private set => Set(ref _status, value); }

    public override Task LoadAsync()
    {
        Rows.Clear();
        var bindings = AppServices.Hotkeys.GetAll();
        if (bindings.Count == 0) bindings = HotkeyDefaults.Create();

        // The old "activate specific profile" row had no profile target and no
        // registered handler. Existing saved entries are ignored until the action
        // has a target picker.
        foreach (var binding in bindings.Where(b => b.Action != HotkeyAction.ActivateProfile))
        {
            Rows.Add(new HotkeyRow(binding));
        }
        RefreshErrors();
        return Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        var specs = new List<HotkeyBinding>();
        var failures = 0;

        foreach (var row in Rows)
        {
            if (!row.Enabled)
            {
                row.Error = string.Empty;
                specs.Add(row.Binding with { Enabled = false });
                continue;
            }

            if (!HotkeyCatalog.TryParse(row.Text, out var spec, out var error))
            {
                row.Error = error;
                failures++;
                continue;
            }

            row.Error = string.Empty;
            specs.Add(row.Binding with { Spec = spec, Enabled = true });
        }

        if (failures > 0)
        {
            Status = $"{failures} shortcut{(failures == 1 ? " was" : "s were")} not saved because the combination is invalid.";
            return;
        }

        var conflicts = HotkeyCatalog.FindConflicts(specs);
        if (conflicts.Count > 0)
        {
            Status = conflicts[0];
            return;
        }

        AppServices.Hotkeys.Save(specs);
        AppServices.SaveSettings(s => s);
        Status = "Hotkeys saved and registered.";
        await Task.CompletedTask;
    }

    private void RefreshErrors()
    {
        foreach (var row in Rows) row.Error = string.Empty;
    }
}
