using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private async void Apply_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PresetCardViewModel card } &&
            DataContext is LibraryViewModel vm)
        {
            await vm.ApplyAsync(card);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: PresetCardViewModel card } &&
            DataContext is LibraryViewModel vm)
        {
            await vm.DeleteAsync(card);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm) await vm.SaveCurrentAsync();
    }
}
