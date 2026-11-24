using System.Windows;
using System.Windows.Controls;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class CustomizeView : UserControl
{
    public CustomizeView()
    {
        InitializeComponent();
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomizeViewModel vm) vm.ChooseBackground();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomizeViewModel vm) vm.ClearBackground();
    }
}
