using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Veyra.UI.Infrastructure;
using Veyra.UI.Shell;

namespace Veyra.UI;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnUnhandled;

        try
        {
            await AppServices.InitializeAsync(default);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Veyra could not start: " + ex.Message, "Veyra", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Veyra hit an unexpected error: " + e.Exception.Message, "Veyra", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (AppServices.Settings.RestoreOnExit)
        {
            try
            {
                var displays = AppServices.Displays.GetDisplaysAsync(default).GetAwaiter().GetResult();
                foreach (var display in displays)
                {
                    AppServices.Colour.RestoreAsync(display, default).GetAwaiter().GetResult();
                }
            }
            catch (Exception)
            {
                // Shutdown must not fail because a driver refused a restore.
            }
        }

        AppServices.Overlay.Dispose();
        AppServices.Colour.Dispose();
        base.OnExit(e);
    }
}
