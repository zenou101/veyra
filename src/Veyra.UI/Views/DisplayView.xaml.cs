using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class DisplayView : UserControl
{
    public DisplayView()
    {
        InitializeComponent();
    }

    private void Display_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Veyra.Core.Models.DisplayInfo display } &&
            DataContext is DisplayViewModel vm)
        {
            vm.SelectedDisplay = display;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayViewModel vm) await vm.RestoreAsync();
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayViewModel vm) await vm.ResetAsync();
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayViewModel vm) await vm.SavePresetAsync();
    }

    private async void Preset_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PresetCardViewModel card } &&
            DataContext is DisplayViewModel vm)
        {
            await vm.ApplyPresetAsync(card);
        }
    }
}
