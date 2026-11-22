using Veyra.Core.Models;

namespace Veyra.Core.Services;

/// <summary>
/// Configuration presets only. They are not a performance guarantee and the UI says so.
/// </summary>
public static class BuiltInPresets
{
    public static IReadOnlyList<Preset> Create() => new[]
    {
        new Preset
        {
            Id = "builtin.default",
            Name = "Default",
            Description = "Neutral output with no ramp adjustment.",
            Kind = PresetKind.Colour,
            Colour = ColourSettings.Neutral,
            IsBuiltIn = true,
            IsDefault = true
        },
        new Preset
        {
            Id = "builtin.balanced",
            Name = "Balanced",
            Description = "A small lift in colour presence without washing out the desktop.",
            Colour = new ColourSettings { Gamma = 1.05, Brightness = 0, Contrast = 3, Red = 0, Green = 0, Blue = 0, Temperature = 6500 },
            IsBuiltIn = true
        },
        new Preset
        {
            Id = "builtin.vibrant",
            Name = "Vibrant",
            Description = "Strong true saturation for games that look flat, through the AMD or NVIDIA driver colour control.",
            Colour = new ColourSettings { Gamma = 1.12, Contrast = 8, Brightness = 1, Red = 4, Green = 0, Blue = 4, Temperature = 6500, Saturation = 160 },
            IsBuiltIn = true
        },
        new Preset
        {
            Id = "builtin.competitive",
            Name = "Competitive",
            Description = "Higher contrast with lifted shadows for visibility.",
            Colour = new ColourSettings { Gamma = 0.92, Contrast = 14, Brightness = 2, Shadow = 12, Temperature = 6800 },
            IsBuiltIn = true
        },
        new Preset
        {
            Id = "builtin.warm",
            Name = "Warm",
            Description = "Lower colour temperature for evening use.",
            Colour = new ColourSettings { Temperature = 5400, Gamma = 1.02, Brightness = -2 },
            IsBuiltIn = true
        },
        new Preset
        {
            Id = "builtin.cool",
            Name = "Cool",
            Description = "Higher colour temperature for a cleaner white point.",
            Colour = new ColourSettings { Temperature = 7600, Gamma = 1.0, Brightness = 0 },
            IsBuiltIn = true
        },
        new Preset
        {
            Id = "builtin.night",
            Name = "Night",
            Description = "Lower brightness and warmer white point for dark rooms.",
            Colour = new ColourSettings { Temperature = 4200, Brightness = -18, Contrast = -6, Gamma = 1.08 },
            IsBuiltIn = true
        }
    };
}
