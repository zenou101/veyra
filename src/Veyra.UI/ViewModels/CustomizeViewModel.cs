using System;
using System.IO;
using System.Windows.Media;
using Microsoft.Win32;
using Veyra.UI.Infrastructure;
using Color = System.Windows.Media.Color;

namespace Veyra.UI.ViewModels;

public sealed class CustomizeViewModel : PageViewModel
{
    private double _panelOpacityPercent = AppServices.Settings.PanelOpacity * 100;
    private string _appearanceMode = AppServices.Settings.AppearanceMode;
    private string _backdrop = AppServices.Settings.Backdrop;
    private string _status = "Changes apply immediately and are saved for the next launch.";

    public override string Title => "Customize";
    public override string Subtitle => "How Veyra itself looks. The colour of your games is set on Display.";
    public override string Glyph => "\uE771";

    public string Status { get => _status; private set => Set(ref _status, value); }

    public double PanelOpacityPercent
    {
        get => _panelOpacityPercent;
        set
        {
            if (!Set(ref _panelOpacityPercent, value)) return;
            OnPropertyChanged(nameof(PanelOpacityDetail));
            AppServices.SaveSettings(s => s with { PanelOpacity = value / 100.0 });
            AppearanceService.Apply();
            Status = $"Panel opacity set to {value:0} %.";
        }
    }

    public string PanelOpacityDetail => $"{PanelOpacityPercent:0} %";

    public bool IsSlate
    {
        get => string.Equals(_appearanceMode, "Slate", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) SetMode("Slate");
        }
    }

    public bool IsDefaultMode
    {
        get => !string.Equals(_appearanceMode, "Slate", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) SetMode("Default");
        }
    }

    public string AppearanceModeDetail => IsSlate ? "Slate (see-through)" : "Default";

    public bool IsBackdropOff
    {
        get => string.Equals(_backdrop, "Off", StringComparison.OrdinalIgnoreCase);
        set { if (value) SetBackdrop("Off"); }
    }

    public bool IsBackdropSoft
    {
        get => string.Equals(_backdrop, "Soft", StringComparison.OrdinalIgnoreCase);
        set { if (value) SetBackdrop("Soft"); }
    }

    public bool IsBackdropPetals
    {
        get => string.Equals(_backdrop, "Petals", StringComparison.OrdinalIgnoreCase);
        set { if (value) SetBackdrop("Petals"); }
    }

    public bool HasBackground => !string.IsNullOrWhiteSpace(AppServices.Settings.BackgroundImagePath) &&
                                 File.Exists(AppServices.Settings.BackgroundImagePath);

    public string BackgroundLabel => HasBackground
        ? Path.GetFileName(AppServices.Settings.BackgroundImagePath)!
        : "None";

    public double ContrastRatio
    {
        get
        {
            var panel = IsSlate ? Color.FromRgb(47, 47, 47) : Color.FromRgb(59, 59, 59);
            var text = Color.FromRgb(246, 244, 238);
            static double Channel(byte value)
            {
                var v = value / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            var l1 = 0.2126 * Channel(text.R) + 0.7152 * Channel(text.G) + 0.0722 * Channel(text.B);
            var l2 = 0.2126 * Channel(panel.R) + 0.7152 * Channel(panel.G) + 0.0722 * Channel(panel.B);
            return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
        }
    }

    public string ContrastLabel => $"{ContrastRatio:0.0} : 1";
    public string ContrastVerdict => ContrastRatio >= 4.5
        ? "Readable against the panel."
        : "Below the 4.5 : 1 target. Raise panel opacity.";

    private void SetMode(string mode)
    {
        _appearanceMode = mode;
        AppServices.SaveSettings(s => s with { AppearanceMode = mode });
        AppearanceService.Apply();
        OnPropertyChanged(nameof(IsSlate));
        OnPropertyChanged(nameof(IsDefaultMode));
        OnPropertyChanged(nameof(AppearanceModeDetail));
        OnPropertyChanged(nameof(ContrastRatio));
        OnPropertyChanged(nameof(ContrastLabel));
        OnPropertyChanged(nameof(ContrastVerdict));
        Status = mode == "Slate" ? "Slate panels enabled." : "Default panels enabled.";
    }

    private void SetBackdrop(string backdrop)
    {
        _backdrop = backdrop;
        AppServices.SaveSettings(s => s with { Backdrop = backdrop });
        AppearanceService.Apply();
        OnPropertyChanged(nameof(IsBackdropOff));
        OnPropertyChanged(nameof(IsBackdropSoft));
        OnPropertyChanged(nameof(IsBackdropPetals));
        Status = backdrop == "Off" ? "Backdrop off." : $"{backdrop} backdrop enabled.";
    }

    public void ChooseBackground()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a background picture",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        AppServices.SaveSettings(s => s with { BackgroundImagePath = dialog.FileName });
        AppearanceService.Apply();
        RaiseBackground();
        Status = "Background picture applied.";
    }

    public void ClearBackground()
    {
        AppServices.SaveSettings(s => s with { BackgroundImagePath = null });
        AppearanceService.Apply();
        RaiseBackground();
        Status = "Background picture removed.";
    }

    private void RaiseBackground()
    {
        OnPropertyChanged(nameof(HasBackground));
        OnPropertyChanged(nameof(BackgroundLabel));
    }
}
