using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Views;

public partial class CrosshairView : UserControl
{
    public CrosshairView()
    {
        InitializeComponent();
    }

    private void Colour_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string colour } && DataContext is CrosshairViewModel vm)
        {
            vm.Colour = colour;
        }
    }
}
