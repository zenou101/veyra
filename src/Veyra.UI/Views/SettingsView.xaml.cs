using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) await vm.OpenDataFolderAsync();
    }
}
