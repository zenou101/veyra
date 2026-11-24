using System;
using System.Windows;
using System.Windows.Media;
using Veyra.Core.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace Veyra.UI.Controls;

/// <summary>Draws the same geometry as the overlay so the page can show a true preview.</summary>
public sealed class CrosshairPreview : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(CrosshairSettings), typeof(CrosshairPreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public CrosshairSettings? Settings
    {
        get => (CrosshairSettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var settings = Settings ?? CrosshairSettings.Default;
        var colour = Parse(settings.Colour);
        var x = RenderSize.Width / 2;
        var y = RenderSize.Height / 2;
        var half = Math.Max(3, settings.Size);
        var thickness = Math.Max(1, settings.Thickness);
        var gap = Math.Max(0, settings.Gap);

        var fill = new SolidColorBrush(colour) { Opacity = Math.Clamp(settings.Opacity, 0.05, 1) };
        fill.Freeze();
        var pen = new Pen(fill, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();

        Pen? outline = null;
        if (settings.Outline)
        {
            outline = new Pen(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)), thickness + 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            outline.Freeze();
        }

        void Line(double x1, double y1, double x2, double y2)
        {
            if (outline is not null) dc.DrawLine(outline, new Point(x1, y1), new Point(x2, y2));
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }

        switch (settings.Shape)
        {
            case CrosshairShape.Dot:
                dc.DrawEllipse(fill, null, new Point(x, y), Math.Max(2, thickness * 1.4), Math.Max(2, thickness * 1.4));
                break;

            case CrosshairShape.Circle:
                if (outline is not null) dc.DrawEllipse(null, outline, new Point(x, y), half, half);
                dc.DrawEllipse(null, pen, new Point(x, y), half, half);
                break;

            case CrosshairShape.CrossWithGap:
                Line(x - half, y, x - gap, y);
                Line(x + gap, y, x + half, y);
                Line(x, y - half, x, y - gap);
                Line(x, y + gap, x, y + half);
                break;

            case CrosshairShape.TShape:
                Line(x - half, y - half, x + half, y - half);
                Line(x, y - half, x, y + half);
                break;

            default:
                Line(x - half, y, x + half, y);
                Line(x, y - half, x, y + half);
                break;
        }
    }

    private static Color Parse(string value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value)!;
        }
        catch (Exception)
        {
            return Color.FromRgb(139, 124, 246);
        }
    }
}
