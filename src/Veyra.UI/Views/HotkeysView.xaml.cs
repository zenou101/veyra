using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class HotkeysView : UserControl
{
    public HotkeysView()
    {
        InitializeComponent();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HotkeysViewModel vm)
        {
            await vm.SaveAsync();
            if (Window.GetWindow(this) is Veyra.UI.Shell.MainWindow shell) shell.ReloadHotkeys();
        }
    }
}
