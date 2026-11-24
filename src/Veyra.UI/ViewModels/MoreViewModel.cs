using System.Threading.Tasks;

namespace Veyra.UI.ViewModels;

public sealed class MoreViewModel : PageViewModel
{
    public MoreViewModel()
    {
        Settings = new SettingsViewModel();
        Hotkeys = new HotkeysViewModel();
    }

    public override string Title => "More";
    public override string Subtitle => "Application behaviour, shortcuts and where your data lives.";
    public override string Glyph => "\uE712";

    public SettingsViewModel Settings { get; }
    public HotkeysViewModel Hotkeys { get; }

    public string Version => Veyra.Core.AppInfo.Version;
    public string Licence => "GPL-3.0-or-later";
    public string DataFolder => Infrastructure.AppServices.DataFolder;
    public string ProviderDetail => Infrastructure.AppServices.Colour.VendorDetail;
    public string StartupStatus => Infrastructure.AppServices.StartupStatus;

    public override async Task LoadAsync()
    {
        await Hotkeys.LoadAsync();
    }
}
