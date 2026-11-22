using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class ColoursView : UserControl
{
    public ColoursView()
    {
        InitializeComponent();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e) { if (DataContext is ColoursViewModel vm) await vm.ApplyAsync(); }
    private async void Restore_Click(object sender, RoutedEventArgs e) { if (DataContext is ColoursViewModel vm) await vm.RestoreAsync(); }
    private async void Preset_Click(object sender, RoutedEventArgs e) { if (DataContext is ColoursViewModel vm) await vm.ApplyPresetAsync(); }
    private async void SavePreset_Click(object sender, RoutedEventArgs e) { if (DataContext is ColoursViewModel vm) await vm.SavePresetAsync(); }
    private async void DeletePreset_Click(object sender, RoutedEventArgs e) { if (DataContext is ColoursViewModel vm) await vm.DeletePresetAsync(); }
}
