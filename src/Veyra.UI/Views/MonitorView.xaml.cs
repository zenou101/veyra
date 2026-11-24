using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        InitializeComponent();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm) await vm.RestoreAsync();
    }
}
