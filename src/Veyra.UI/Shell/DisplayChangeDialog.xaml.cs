using System;
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
    private bool _resolved;

    public DisplayChangeDialog(DisplayInfo display, DisplayMode mode)
    {
        InitializeComponent();
        _display = display;
        _mode = mode;

        SummaryText.Text = $"{display.FriendlyName} → {mode.Label}";
        DetailText.Text = "If you do not confirm, Veyra restores the previous mode automatically. The recovery guard runs as a separate process.";
        UpdateCountdown();

        Loaded += async (_, _) => await ApplyAsync();
        _timer.Tick += async (_, _) => await TickAsync();
        Closing += (_, _) =>
        {
            _timer.Stop();
            _guard?.Dispose();
        };
    }

    public string ResultMessage { get; private set; } = "No change was made.";

    private async Task ApplyAsync()
    {
        _state = await AppServices.Displays.CaptureStateAsync(_display.Id, default);
        if (_state is null)
        {
            ResultMessage = "The current display state could not be captured, so the change was blocked.";
            Close();
            return;
        }

        _guard = DisplayGuardSession.Start(new[] { _state }, 25, out var guardError);
        if (_guard is null)
        {
            ResultMessage = guardError;
            Close();
            return;
        }

        var result = await AppServices.Displays.ApplyModeAsync(_display, _mode, default);
        if (!result.IsSuccess)
        {
            _guard.Confirm();
            ResultMessage = result.Message;
            Close();
            return;
        }

        _timer.Start();
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
        ResultMessage = message;
        Close();
    }

    private async void Revert_Click(object sender, RoutedEventArgs e) =>
        await RevertAsync("You reverted the display change; the previous mode is back.");

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        if (_resolved) return;
        _resolved = true;
        _timer.Stop();
        _guard?.Confirm();
        ResultMessage = $"Confirmed: {_display.FriendlyName} is now {_mode.Label}.";
        Close();
    }
}
