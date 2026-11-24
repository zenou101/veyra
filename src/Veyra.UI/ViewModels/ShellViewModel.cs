using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed record NavigationEntry(PageViewModel Page)
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
        Display = new DisplayViewModel();
        Library = new LibraryViewModel();
        Resolution = new ResolutionViewModel();
        Monitor = new MonitorViewModel();
        Customize = new CustomizeViewModel();
        Crosshair = new CrosshairViewModel();
        Profiles = new ProfilesViewModel();
        More = new MoreViewModel();

        MoreEntry = new NavigationEntry(More);
        MainNavigation = new[]
        {
            new NavigationEntry(Display),
            new NavigationEntry(Library),
            new NavigationEntry(Resolution),
            new NavigationEntry(Monitor),
            new NavigationEntry(Customize),
            new NavigationEntry(Crosshair),
            new NavigationEntry(Profiles)
        };
        NavigationEntries = new List<NavigationEntry>(MainNavigation) { MoreEntry };

        _currentPage = Display;
        _selectedNavigationEntry = MainNavigation[0];

        AppearanceService.Changed += OnAppearanceChanged;
    }

    public DisplayViewModel Display { get; }
    public LibraryViewModel Library { get; }
    public ResolutionViewModel Resolution { get; }
    public MonitorViewModel Monitor { get; }
    public CustomizeViewModel Customize { get; }
    public CrosshairViewModel Crosshair { get; }
    public ProfilesViewModel Profiles { get; }
    public MoreViewModel More { get; }

    public IReadOnlyList<NavigationEntry> MainNavigation { get; }
    public IReadOnlyList<NavigationEntry> NavigationEntries { get; }
    public NavigationEntry MoreEntry { get; }

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

            OnPropertyChanged(nameof(IsMoreSelected));
            _ = value.LoadAsync();
            Status = value.Title + " · " + AppServices.StartupStatus;
        }
    }

    public bool IsMoreSelected => ReferenceEquals(CurrentPage, More);

    public void SelectMore()
    {
        SelectedNavigationEntry = MoreEntry;
    }

    public string Status { get => _status; set => Set(ref _status, value); }
    public string Version => Veyra.Core.AppInfo.Version;
    public string Title => "Veyra";

    public bool ShowSoftBackdrop => string.Equals(AppServices.Settings.Backdrop, "Soft", StringComparison.OrdinalIgnoreCase);
    public bool ShowPetalBackdrop => string.Equals(AppServices.Settings.Backdrop, "Petals", StringComparison.OrdinalIgnoreCase);

    private string? _backgroundPath;
    private ImageSource? _backgroundImage;

    public ImageSource? BackgroundImage
    {
        get
        {
            var path = AppServices.Settings.BackgroundImagePath;
            if (string.Equals(path, _backgroundPath, StringComparison.OrdinalIgnoreCase)) return _backgroundImage;

            _backgroundPath = path;
            _backgroundImage = null;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                _backgroundImage = image;
            }
            catch (Exception)
            {
                // A broken image must not stop the shell from loading.
            }

            return _backgroundImage;
        }
    }

    private void OnAppearanceChanged()
    {
        OnPropertyChanged(nameof(ShowSoftBackdrop));
        OnPropertyChanged(nameof(ShowPetalBackdrop));
        OnPropertyChanged(nameof(BackgroundImage));
    }

    public async Task InitializeAsync()
    {
        Status = AppServices.StartupStatus;
        AppearanceService.Apply();
        await Display.LoadAsync();
    }
}
