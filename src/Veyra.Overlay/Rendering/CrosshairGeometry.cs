using System.Windows;
using System.Windows.Media;
using Veyra.Core.Models;

namespace Veyra.Overlay.Rendering;

/// <summary>Pure geometry for the crosshair, kept separate so it can be unit tested.</summary>
public static class CrosshairGeometry
{
    public static IReadOnlyList<Rect> BuildStrokes(CrosshairSettings settings, Point centre)
    {
        var half = Math.Max(1, settings.Size / 2);
        var gap = Math.Max(0, settings.Gap);
        var thickness = Math.Max(0.5, settings.Thickness);
        var length = Math.Max(0, half - gap);
        if (length <= 0) return Array.Empty<Rect>();

        return settings.Shape switch
        {
            CrosshairShape.Dot => new[] { Square(centre, thickness) },
            CrosshairShape.Cross => new[]
            {
                new Rect(centre.X - half, centre.Y - thickness / 2, half * 2, thickness),
                new Rect(centre.X - thickness / 2, centre.Y - half, thickness, half * 2)
            },
            CrosshairShape.CrossWithGap => new[]
            {
                new Rect(centre.X - half, centre.Y - thickness / 2, length, thickness),
                new Rect(centre.X + gap, centre.Y - thickness / 2, length, thickness),
                new Rect(centre.X - thickness / 2, centre.Y - half, thickness, length),
                new Rect(centre.X - thickness / 2, centre.Y + gap, thickness, length)
            },
            CrosshairShape.TShape => new[]
            {
                new Rect(centre.X - half, centre.Y - thickness / 2, half * 2, thickness),
                new Rect(centre.X - thickness / 2, centre.Y + gap, thickness, length)
            },
            CrosshairShape.Circle => Array.Empty<Rect>(),
            _ => Array.Empty<Rect>()
        };
    }

    public static double CircleRadius(CrosshairSettings settings) => Math.Max(1, settings.Size / 2);

    private static Rect Square(Point centre, double size) =>
        new(centre.X - size / 2, centre.Y - size / 2, size, size);

    public static Color ParseColour(string value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return Color.FromRgb(0x8B, 0x7C, 0xF6);
        }
    }
}
