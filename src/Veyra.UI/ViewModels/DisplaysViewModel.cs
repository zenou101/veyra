using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class DisplayRowViewModel : ObservableObject
{
    public DisplayRowViewModel(DisplayInfo info)
    {
        Info = info;
    }

    public DisplayInfo Info { get; }
    public string Name => Info.FriendlyName;
    public string Summary => $"{Info.ResolutionLabel} · {Info.RefreshLabel} · {Info.Orientation}";
    public string Adapter => Info.AdapterName;
    public string Hdr => Info.HdrSupported ? (Info.HdrEnabled ? "HDR on" : "HDR available") : "HDR not supported";
    public bool IsPrimary => Info.IsPrimary;
    public ObservableCollection<DisplayMode> Modes { get; } = new();

    private DisplayMode? _selectedMode;
    public DisplayMode? SelectedMode { get => _selectedMode; set => Set(ref _selectedMode, value); }

    private bool _busy;
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }

    public string PrimaryBadge => IsPrimary ? "PRIMARY" : "SECONDARY";
}

public sealed class DisplaysViewModel : PageViewModel
{
    private DisplayRowViewModel? _selected;
    private string _status = "Read-only until you confirm a change.";
    private bool _guardReady = true;

    public override string Title => "Displays";
    public override string Subtitle => "Connected monitors, modes and HDR capability. Every change needs your confirmation.";
    public override string Glyph => "\uE7F4";

    public ObservableCollection<DisplayRowViewModel> Displays { get; } = new();

    public DisplayRowViewModel? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool GuardAvailable { get => _guardReady; private set => Set(ref _guardReady, value); }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var displays = await AppServices.Displays.GetDisplaysAsync(cancellationToken);
            Displays.Clear();

            foreach (var display in displays)
            {
                var row = new DisplayRowViewModel(display);
                var modes = await AppServices.Displays.GetModesAsync(display.Id, cancellationToken);
                foreach (var mode in modes
                    .Where(m => m.Width == display.Width && m.Height == display.Height)
                    .OrderByDescending(m => m.RefreshRate)
                    .Take(12))
                {
                    row.Modes.Add(mode);
                }

                row.SelectedMode = row.Modes.FirstOrDefault(m => m.RefreshRate == display.RefreshRate) ?? row.Modes.FirstOrDefault();
                Displays.Add(row);
            }

            Selected ??= Displays.FirstOrDefault();
            Status = Displays.Count == 0
                ? "No displays were detected."
                : $"{Displays.Count} display{(Displays.Count == 1 ? string.Empty : "s")} detected. Modes were read from Windows.";
        }
        catch (Exception ex)
        {
            Status = "Display enumeration failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public override Task LoadAsync() => LoadAsync(CancellationToken.None);

    public void ReportStatus(string message) => Status = message;
}
