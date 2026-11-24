using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class MoreView : UserControl
{
    public MoreView()
    {
        InitializeComponent();
    }

    private async void SaveHotkeys_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoreViewModel vm)
        {
            await vm.Hotkeys.SaveAsync();
            if (Window.GetWindow(this) is Shell.MainWindow window) window.ReloadHotkeys();
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoreViewModel vm) await vm.Settings.OpenDataFolderAsync();
    }
}
