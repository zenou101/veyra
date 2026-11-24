using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Veyra.Core.Models;
using Veyra.Display.Recovery;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.Shell;

public partial class DisplayChangeDialog : Window
{
    private readonly DisplayInfo _display;
    private readonly DisplayMode _mode;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DisplayState? _state;
    private DisplayGuardSession? _guard;
    private int _remaining = 15;
    private bool _applying = true;
    private bool _applied;
    private bool _resolved;
    private bool _allowClose;
    private bool _closeRequested;

    public DisplayChangeDialog(DisplayInfo display, DisplayMode mode)
    {
        InitializeComponent();
        _display = display;
        _mode = mode;

        SummaryText.Text = $"{display.FriendlyName} → {mode.Label}";
        DetailText.Text = "If you do not confirm, Veyra restores the previous mode automatically. The recovery guard runs as a separate process.";
        RevertButton.IsEnabled = false;
        KeepButton.IsEnabled = false;
        CountdownText.Text = "Applying the mode…";

        Loaded += async (_, _) => await ApplyAsync();
        _timer.Tick += async (_, _) => await TickAsync();
        Closing += OnClosing;
    }

    public string ResultMessage { get; private set; } = "No change was made.";

    private async Task ApplyAsync()
    {
        try
        {
            _state = await AppServices.Displays.CaptureStateAsync(_display.Id, default);
            if (_resolved) return;
            if (_state is null)
            {
                Finish("The current display state could not be captured, so the change was blocked.");
                return;
            }

            _guard = DisplayGuardSession.Start(new[] { _state }, 25, out var guardError);
            if (_resolved) return;
            if (_guard is null)
            {
                Finish(guardError);
                return;
            }

            var result = await AppServices.Displays.ApplyModeAsync(_display, _mode, default);
            if (_resolved) return;
            if (!result.IsSuccess)
            {
                _guard.Confirm();
                Finish(result.Message);
                return;
            }

            _applied = true;
            _applying = false;
            RevertButton.IsEnabled = true;
            KeepButton.IsEnabled = true;
            _timer.Start();
            UpdateCountdown();

            if (_closeRequested)
            {
                await RevertAsync("The confirmation window was closed, so the previous mode was restored.");
            }
        }
        catch (Exception ex)
        {
            _guard?.Confirm();
            Finish("The display change failed: " + ex.Message);
        }
        finally
        {
            _applying = false;
        }
    }

    private async Task TickAsync()
    {
        _remaining--;
        UpdateCountdown();

        if (_remaining > 0) return;

        await RevertAsync("The confirmation timed out, so the previous mode was restored.");
    }

    private void UpdateCountdown()
    {
        CountdownText.Text = _remaining > 0
            ? $"Keeping this mode in {_remaining} s"
            : "Reverting now…";
    }

    private async Task RevertAsync(string message)
    {
        if (_resolved) return;
        _resolved = true;
        _timer.Stop();

        if (_state is not null)
        {
            var result = await AppServices.Displays.RestoreStateAsync(_state, default);
            message = result.IsSuccess ? message : message + " Restore reported: " + result.Message;
        }

        _guard?.Confirm();
        Finish(message);
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        if (!_applied || _resolved) return;
        await RevertAsync("You reverted the display change; the previous mode is back.");
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        if (!_applied || _resolved) return;
        _resolved = true;
        _timer.Stop();
        _guard?.Confirm();
        Finish($"Confirmed: {_display.FriendlyName} is now {_mode.Label}.");
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;

        e.Cancel = true;

        // Do not race a revert against ApplyModeAsync. Remember the close request and finish
        // as soon as the apply completes.
        if (_applying)
        {
            _closeRequested = true;
            return;
        }

        if (!_resolved)
        {
            await RevertAsync("The confirmation window was closed, so the previous mode was restored.");
        }
    }

    private void Finish(string message)
    {
        ResultMessage = message;
        _allowClose = true;
        Close();
    }
}
