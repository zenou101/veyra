using System.Windows;
using System.Windows.Controls;
using Veyra.UI.Shell;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class DisplaysView : UserControl
{
    public DisplaysView()
    {
        InitializeComponent();
    }

    private void ApplyMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DisplayRowViewModel row }) return;
        if (row.SelectedMode is null)
        {
            MessageBox.Show("Choose a refresh rate first.", "Veyra", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.SelectedMode.RefreshRate == row.Info.RefreshRate)
        {
            MessageBox.Show("That refresh rate is already active on this display.", "Veyra", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new DisplayChangeDialog(row.Info, row.SelectedMode)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (DataContext is DisplaysViewModel vm) vm.ReportStatus(dialog.ResultMessage);
    }
}
