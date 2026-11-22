using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed record NavigationEntry(string Group, PageViewModel Page)
{
    public string Title => Page.Title;
    public string Glyph => Page.Glyph;
}

public sealed class ShellViewModel : ObservableObject
{
    private NavigationEntry? _selectedNavigationEntry;
    private PageViewModel? _currentPage;
    private string _status = AppServices.StartupStatus;

    public ShellViewModel()
    {
        Overview = new OverviewViewModel();
        Displays = new DisplaysViewModel();
        Colours = new ColoursViewModel();
        Crosshair = new CrosshairViewModel();
        Performance = new PerformanceViewModel();
        Hotkeys = new HotkeysViewModel();
        Settings = new SettingsViewModel();

        NavigationEntries = new[]
        {
            new NavigationEntry("WORKSPACE", Overview),
            new NavigationEntry("WORKSPACE", Displays),
            new NavigationEntry("WORKSPACE", Colours),
            new NavigationEntry("TOOLS", Crosshair),
            new NavigationEntry("TOOLS", Hotkeys),
            new NavigationEntry("SYSTEM", Settings)
        };

        NavigationView = CollectionViewSource.GetDefaultView(NavigationEntries);
        NavigationView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavigationEntry.Group)));

        _currentPage = Overview;
        _selectedNavigationEntry = NavigationEntries[0];
    }

    public OverviewViewModel Overview { get; }
    public DisplaysViewModel Displays { get; }
    public ColoursViewModel Colours { get; }
    public CrosshairViewModel Crosshair { get; }
    public PerformanceViewModel Performance { get; }
    public HotkeysViewModel Hotkeys { get; }
    public SettingsViewModel Settings { get; }

    public IReadOnlyList<NavigationEntry> NavigationEntries { get; }
    public ICollectionView NavigationView { get; }

    public NavigationEntry? SelectedNavigationEntry
    {
        get => _selectedNavigationEntry;
        set
        {
            if (!Set(ref _selectedNavigationEntry, value) || value is null) return;
            CurrentPage = value.Page;
        }
    }

    public PageViewModel? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!Set(ref _currentPage, value) || value is null) return;

            if (!ReferenceEquals(_selectedNavigationEntry?.Page, value))
            {
                foreach (var entry in NavigationEntries)
                {
                    if (!ReferenceEquals(entry.Page, value)) continue;
                    _selectedNavigationEntry = entry;
                    OnPropertyChanged(nameof(SelectedNavigationEntry));
                    break;
                }
            }

            _ = value.LoadAsync();
            Status = value.Title + " · " + AppServices.StartupStatus;
        }
    }

    public string Status { get => _status; set => Set(ref _status, value); }
    public string Version => Veyra.Core.AppInfo.Version;
    public string Title => "Veyra";

    public async Task InitializeAsync()
    {
        Status = AppServices.StartupStatus;
        await Overview.LoadAsync();
    }
}
