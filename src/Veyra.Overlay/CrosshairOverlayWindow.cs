using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Veyra.Core.Models;
using Veyra.Overlay.Rendering;

namespace Veyra.Overlay;

/// <summary>
/// A click-through, non-activating layered window. It draws only the configured
/// crosshair: no injection, no hooks and no interaction with other processes.
/// </summary>
public sealed class CrosshairOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExLayered = 0x80000;
    private const int WsExNoActivate = 0x8000000;

    private readonly DrawingVisual _visual = new();
    private CrosshairSettings _settings;
    private readonly Rect _bounds;

    public CrosshairOverlayWindow(CrosshairSettings settings, Rect physicalBounds)
    {
        _settings = settings;
        _bounds = physicalBounds;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        var host = new DrawingVisualHost(_visual);
        Content = host;
        Render();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate);

        var dpi = GetDpiForWindow(handle) / 96d;
        if (dpi <= 0) dpi = 1;

        Left = _bounds.Left / dpi;
        Top = _bounds.Top / dpi;
        Width = _bounds.Width / dpi;
        Height = _bounds.Height / dpi;
    }

    public void Update(CrosshairSettings settings)
    {
        _settings = settings;
        Render();
    }

    private void Render()
    {
        using var dc = _visual.RenderOpen();
        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        if (centre.X <= 0 || centre.Y <= 0)
        {
            centre = new Point(_bounds.Width / 2, _bounds.Height / 2);
        }

        var colour = CrosshairGeometry.ParseColour(_settings.Colour);
        var brush = new SolidColorBrush(colour) { Opacity = Math.Clamp(_settings.Opacity, 0.05, 1) };
        brush.Freeze();

        var outline = new SolidColorBrush(Colors.Black) { Opacity = 0.55 };
        outline.Freeze();

        if (_settings.Shape == CrosshairShape.Circle)
        {
            var radius = CrosshairGeometry.CircleRadius(_settings);
            if (_settings.Outline)
            {
                dc.DrawEllipse(null, new Pen(outline, _settings.Thickness + _settings.OutlineThickness * 2), centre, radius, radius);
            }

            dc.DrawEllipse(null, new Pen(brush, _settings.Thickness), centre, radius, radius);
            return;
        }

        foreach (var rect in CrosshairGeometry.BuildStrokes(_settings, centre))
        {
            if (_settings.Outline)
            {
                dc.DrawRectangle(outline, null, Inflate(rect, _settings.OutlineThickness));
            }

            dc.DrawRectangle(brush, null, rect);
        }
    }

    private static Rect Inflate(Rect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}

internal sealed class DrawingVisualHost : FrameworkElement
{
    private readonly Visual _child;

    public DrawingVisualHost(Visual child)
    {
        _child = child;
        AddVisualChild(child);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _child;
}
