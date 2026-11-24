using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfilesViewModel vm) vm.PickExecutable();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfilesViewModel vm) await vm.SaveProfileAsync();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GameProfileRow row } &&
            DataContext is ProfilesViewModel vm)
        {
            await vm.ApplyAsync(row);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GameProfileRow row } &&
            DataContext is ProfilesViewModel vm)
        {
            await vm.DeleteAsync(row);
        }
    }
}
