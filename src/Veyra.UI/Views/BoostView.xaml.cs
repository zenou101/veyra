using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class BoostView : UserControl
{
    public BoostView()
    {
        InitializeComponent();
    }

    private async void ResetBoost_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BoostViewModel vm) await vm.ResetDarkBoostAsync();
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BoostViewModel vm) vm.ResetZoom();
    }
}
