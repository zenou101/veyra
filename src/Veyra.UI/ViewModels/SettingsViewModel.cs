using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    private string _status = "Settings are saved as you change them.";
    private bool _restoreOnExit = AppServices.Settings.RestoreOnExit;
    private bool _closeToTray = AppServices.Settings.CloseToTray;
    private bool _startMinimized = AppServices.Settings.StartMinimized;

    public override string Title => "Settings";
    public override string Subtitle => "Application behaviour, colour providers and where your data is stored.";
    public override string Glyph => "\uE713";

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Version => Veyra.Core.AppInfo.Version;
    public string Licence => "GPL-3.0-or-later";
    public string DataFolder => AppServices.DataFolder;
    public string ProviderDetail => AppServices.Colour.VendorDetail;
    public string StartupStatus => AppServices.StartupStatus;

    public bool RestoreOnExit
    {
        get => _restoreOnExit;
        set { if (Set(ref _restoreOnExit, value)) { AppServices.SaveSettings(s => s with { RestoreOnExit = value }); Status = "Restore-on-exit " + (value ? "enabled." : "disabled."); } }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set { if (Set(ref _closeToTray, value)) AppServices.SaveSettings(s => s with { CloseToTray = value }); }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set { if (Set(ref _startMinimized, value)) AppServices.SaveSettings(s => s with { StartMinimized = value }); }
    }


    public async Task OpenDataFolderAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = AppServices.DataFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Status = "The data folder could not be opened: " + ex.Message;
        }

        await Task.CompletedTask;
    }
}
