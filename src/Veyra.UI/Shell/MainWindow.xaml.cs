using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.UI.Infrastructure;
using Veyra.UI.ViewModels;

namespace Veyra.UI.Shell;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;
    private HotkeyService? _hotkeys;
    private NotifyIcon? _tray;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Veyra-256.png"));
        _shell = new ShellViewModel();
        DataContext = _shell;
        _shell.PropertyChanged += OnShellPropertyChanged;

        Width = AppServices.Settings.WindowWidth;
        Height = AppServices.Settings.WindowHeight;
        if (!double.IsNaN(AppServices.Settings.WindowLeft)) Left = AppServices.Settings.WindowLeft;
        if (!double.IsNaN(AppServices.Settings.WindowTop)) Top = AppServices.Settings.WindowTop;
        if (AppServices.Settings.WindowMaximized) WindowState = WindowState.Maximized;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimatePage();
        SetupTray();
        SetupHotkeys();
        await _shell.InitializeAsync();

        if (AppServices.Settings.StartMinimized)
        {
            Hide();
            _tray?.ShowBalloonTip(2000, "Veyra", "Running in the notification area.", ToolTipIcon.Info);
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
        {
            AnimatePage();
        }
    }

    private void AnimatePage()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        PageMove.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        PageMove.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
    }

    private void SetupHotkeys()
    {
        _hotkeys = new HotkeyService(this);
        _hotkeys.Pressed += OnHotkey;
        ReloadHotkeys();
    }

    public void ReloadHotkeys()
    {
        var bindings = AppServices.Hotkeys.GetAll();
        if (bindings.Count == 0) bindings = HotkeyDefaults.Create();
        _hotkeys?.Register(bindings);
    }

    private void OnHotkey(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleCrosshair:
                _shell.Crosshair.Toggle();
                break;
            case HotkeyAction.OpenVeyra:
                Show();
                WindowState = WindowState.Normal;
                Activate();
                break;
            case HotkeyAction.ToggleColourEnhancements:
                _ = ToggleColourAsync();
                break;
            case HotkeyAction.RestoreDefaults:
                _ = _shell.Colours.RestoreAsync();
                break;
            case HotkeyAction.NextPreset:
                _ = NextPresetAsync();
                break;
        }
    }

    private async System.Threading.Tasks.Task ToggleColourAsync()
    {
        var display = _shell.Colours.SelectedDisplay;
        if (display is null) return;

        var current = await AppServices.Colour.GetCurrentStateAsync(display, default);
        var neutral = current.Values.Gamma is null or 1 && current.Values.Brightness is null or 0;
        _ = neutral
            ? await AppServices.Colour.ApplyAsync(display, BuiltInPresets.Create().First(p => p.Id == "builtin.balanced").Colour, default)
            : await AppServices.Colour.RestoreAsync(display, default);
    }

    private async System.Threading.Tasks.Task NextPresetAsync()
    {
        var display = _shell.Colours.SelectedDisplay;
        if (display is null) return;

        var presets = await AppServices.Presets.GetAllAsync(default);
        if (presets.Count == 0) return;

        var index = presets.ToList().FindIndex(p => p.Id == _shell.Colours.SelectedPreset?.Id);
        var next = presets[(index + 1) % presets.Count];
        await AppServices.Colour.ApplyAsync(display, next.Colour, default);
    }

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Veyra " + Veyra.Core.AppInfo.Version
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Veyra", null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add("Toggle crosshair", null, (_, _) => _shell.Crosshair.Toggle());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); Activate(); };
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/Veyra.ico"));
            if (resource is not null)
            {
                using var buffer = new System.IO.MemoryStream();
                using (var stream = resource.Stream) stream.CopyTo(buffer);
                buffer.Position = 0;
                return new System.Drawing.Icon(buffer, SystemInformation.SmallIconSize);
            }
        }
        catch (Exception)
        {
            // A missing or malformed icon must not stop the tray from working.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void ExitApplication()
    {
        _exiting = true;
        _tray?.Dispose();
        _tray = null;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        AppServices.SaveSettings(s => s with
        {
            WindowWidth = Width,
            WindowHeight = Height,
            WindowLeft = Left,
            WindowTop = Top,
            WindowMaximized = WindowState == WindowState.Maximized
        });

        if (!_exiting && AppServices.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _tray?.ShowBalloonTip(1500, "Veyra is still running", "Use the tray icon to reopen or exit.", ToolTipIcon.Info);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _shell.Performance.Stop();
    }

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
