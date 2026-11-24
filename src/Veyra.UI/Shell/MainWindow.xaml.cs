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
    private double _baseWidth;
    private double _baseHeight;
    private double _appliedZoom;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Veyra-256.png"));
        _shell = new ShellViewModel();
        DataContext = _shell;
        _shell.PropertyChanged += OnShellPropertyChanged;

        _baseWidth = AppServices.Settings.WindowWidth;
        _baseHeight = AppServices.Settings.WindowHeight;
        _appliedZoom = Math.Clamp(_shell.ZoomScale, 0.8, 1.25);
        Width = _baseWidth * _appliedZoom;
        Height = _baseHeight * _appliedZoom;
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

        if (e.PropertyName == nameof(ShellViewModel.ZoomScale))
        {
            ApplyZoom();
        }
    }

    /// <summary>
    /// Interface zoom scales the content layout, so the window grows with it. Window bounds are
    /// saved normalised to 100% and restored at the current zoom, which keeps the saved size stable.
    /// </summary>
    private void ApplyZoom()
    {
        var scale = Math.Clamp(_shell.ZoomScale, 0.8, 1.25);
        if (Math.Abs(scale - _appliedZoom) < 0.001) return;

        _baseWidth = Width / _appliedZoom;
        _baseHeight = Height / _appliedZoom;

        var work = SystemParameters.WorkArea;
        var targetWidth = Math.Max(MinWidth, Math.Min(_baseWidth * scale, work.Width));
        var targetHeight = Math.Max(MinHeight, Math.Min(_baseHeight * scale, work.Height));
        var previousWidth = Width;
        var previousHeight = Height;

        Width = targetWidth;
        Height = targetHeight;
        Left = Math.Max(work.Left, Math.Min(Left + (previousWidth - targetWidth) / 2, work.Right - targetWidth));
        Top = Math.Max(work.Top, Math.Min(Top + (previousHeight - targetHeight) / 2, work.Bottom - targetHeight));
        _appliedZoom = scale;
    }

    private void AnimatePage()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        PageMove.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        PageMove.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
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
                _ = _shell.Display.RestoreAsync();
                break;
            case HotkeyAction.NextPreset:
                _ = _shell.Display.CyclePresetAsync();
                break;
        }
    }

    private async System.Threading.Tasks.Task ToggleColourAsync()
    {
        var display = _shell.Display.SelectedDisplay;
        if (display is null) return;

        var current = await AppServices.Colour.GetCurrentStateAsync(display, default);
        var neutral = current.Values.Gamma is null or 1 && current.Values.Brightness is null or 0;
        _ = neutral
            ? await AppServices.Colour.ApplyAsync(display,
                BuiltInPresets.Create().First(p => p.Id == "builtin.balanced").Colour, default)
            : await AppServices.Colour.RestoreAsync(display, default);
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

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var normal = WindowState == WindowState.Normal;
        AppServices.SaveSettings(s => s with
        {
            WindowWidth = normal ? Width / _appliedZoom : s.WindowWidth,
            WindowHeight = normal ? Height / _appliedZoom : s.WindowHeight,
            WindowLeft = Left,
            WindowTop = Top,
            WindowMaximized = WindowState == WindowState.Maximized,
            LastPage = _shell.CurrentPage?.Title ?? s.LastPage
        });

        if (!_exiting && AppServices.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _tray?.ShowBalloonTip(1500, "Veyra is still running", "Use the tray icon to reopen or exit.", ToolTipIcon.Info);
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
    }

    private void More_Click(object sender, RoutedEventArgs e) => _shell.SelectMore();

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
