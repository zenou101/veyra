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

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: PresetCardViewModel card } &&
            DataContext is DisplayViewModel vm)
        {
            await vm.DeletePresetAsync(card);
        }
    }

    private async void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PresetCardViewModel card } &&
            DataContext is DisplayViewModel vm)
        {
            await vm.ApplyPresetAsync(card);
        }
    }
}
