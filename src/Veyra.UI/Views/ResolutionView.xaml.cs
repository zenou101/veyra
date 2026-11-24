using System.Windows;
using System.Windows.Controls;
using Veyra.UI.Shell;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class ResolutionView : UserControl
{
    public ResolutionView()
    {
        InitializeComponent();
    }

    private void Highest_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ResolutionViewModel vm) vm.SelectHighestRefresh();
    }

    private void Balanced_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ResolutionViewModel vm) vm.SelectClosestRefresh(120);
    }

    private void Battery_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ResolutionViewModel vm) vm.SelectClosestRefresh(60);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResolutionViewModel vm || vm.SelectedDisplay is null) return;

        if (vm.IsSelectedModeCurrent())
        {
            vm.ReportStatus("That mode is already active on this display.");
            return;
        }

        var mode = vm.BuildSelectedMode();
        if (mode is null)
        {
            vm.ReportStatus("Choose a resolution and refresh rate first.");
            return;
        }

        var dialog = new DisplayChangeDialog(vm.SelectedDisplay, mode)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        vm.ReportStatus(dialog.ResultMessage);
        await vm.LoadAsync();
    }
}
