using System.Text.Json;
using System.Text.Json.Serialization;
using Veyra.Core.Models;

namespace Veyra.Core.Services;

public sealed record ProfileDocument
{
    public string Schema { get; init; } = ProfileJson.CurrentSchema;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Kind { get; init; } = "Colour";
    public Dictionary<string, double?>? Colour { get; init; }
    public DisplayDocument? Display { get; init; }
    public GameDocument? Game { get; init; }
    public string? MonitorId { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record DisplayDocument
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int RefreshRate { get; init; }
    public int BitsPerPixel { get; init; } = 32;
    public int Orientation { get; init; }
}

public sealed record GameDocument
{
    public string ExecutableName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; }
}

/// <summary>Interchange format for a single game profile. Ids are never imported.</summary>
public sealed record GameProfileDocument
{
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;
    public int Priority { get; init; } = 100;
    public bool AutoSwitch { get; init; } = true;
    public bool AllowDisplayChanges { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Profile interchange. Import is strict: unknown members, oversized input and out-of-range
/// numbers are rejected, and nothing in a profile file is ever executed.
/// </summary>
public static class ProfileJson
{
    public const string CurrentSchema = "veyra.profile/1";
    public const int MaxDocumentBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static string Export(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var colour = new Dictionary<string, double?>();
        foreach (var control in Enum.GetValues<ColourControl>())
        {
            var value = preset.Colour.Get(control);
            if (value is not null) colour[control.ToString()] = value;
        }

        var document = new ProfileDocument
        {
            Name = preset.Name,
            Description = preset.Description,
            Kind = preset.Kind.ToString(),
            Colour = colour,
            Display = preset.DisplayMode is null ? null : new DisplayDocument
            {
                Width = preset.DisplayMode.Width,
                Height = preset.DisplayMode.Height,
                RefreshRate = preset.DisplayMode.RefreshRate,
                BitsPerPixel = preset.DisplayMode.BitsPerPixel,
                Orientation = preset.DisplayMode.Orientation
            },
            MonitorId = preset.MonitorId,
            Metadata = new Dictionary<string, string>
            {
                ["app"] = "Veyra",
                ["version"] = Veyra.Core.AppInfo.Version,
                ["exportedAt"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };

        return JsonSerializer.Serialize(document, Options);
    }

    public static OperationResult Import(string json, out Preset? preset)
    {
        preset = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return OperationResult.Invalid("The file is empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxDocumentBytes)
        {
            return OperationResult.Invalid("The profile is larger than the 256 KB limit.");
        }

        ProfileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ProfileDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            return OperationResult.Invalid("The profile is not valid JSON for Veyra.", ex.Message);
        }

        if (document is null)
        {
            return OperationResult.Invalid("The profile could not be read.");
        }

        if (!string.Equals(document.Schema, CurrentSchema, StringComparison.Ordinal))
        {
            return OperationResult.Invalid($"Unsupported profile schema '{document.Schema}'. Expected '{CurrentSchema}'.");
        }

        var name = (document.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return OperationResult.Invalid("The profile needs a name between 1 and 120 characters.");
        }

        if (!Enum.TryParse<PresetKind>(document.Kind, true, out var kind))
        {
            return OperationResult.Invalid($"Unknown profile kind '{document.Kind}'.");
        }

        var colourSettings = new ColourSettings();
        if (document.Colour is not null)
        {
            foreach (var pair in document.Colour)
            {
                if (!Enum.TryParse<ColourControl>(pair.Key, true, out var control))
                {
                    return OperationResult.Invalid($"Unknown colour control '{pair.Key}'.");
                }

                if (pair.Value is null) continue;
                var range = Ranges[control];
                if (double.IsNaN(pair.Value.Value) || pair.Value.Value < range.Min || pair.Value.Value > range.Max)
                {
                    return OperationResult.Invalid($"{control} must be between {range.Min} and {range.Max}.");
                }

                colourSettings = colourSettings.With(control, pair.Value);
            }
        }

        DisplayMode? mode = null;
        if (document.Display is not null)
        {
            var d = document.Display;
            if (d.Width is < 320 or > 16384 || d.Height is < 200 or > 16384)
            {
                return OperationResult.Invalid("Display width or height is outside the supported range.");
            }

            if (d.RefreshRate is < 1 or > 1000)
            {
                return OperationResult.Invalid("Refresh rate must be between 1 and 1000 Hz.");
            }

            if (d.BitsPerPixel is not (16 or 24 or 32))
            {
                return OperationResult.Invalid("Colour depth must be 16, 24 or 32 bits.");
            }

            if (d.Orientation is < 0 or > 3)
            {
                return OperationResult.Invalid("Orientation must be 0-3.");
            }

            mode = new DisplayMode(d.Width, d.Height, d.RefreshRate, d.BitsPerPixel, d.Orientation);
        }

        if (document.Game is not null)
        {
            var exe = document.Game.ExecutableName;
                if (string.IsNullOrWhiteSpace(exe) || exe.Length > 260 || exe.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || exe.Contains(":"))
            {
                return OperationResult.Invalid("The game executable must be a file name, not a path.");
            }
        }

        preset = new Preset
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            Name = name,
            Description = document.Description,
            Kind = kind,
            Colour = colourSettings,
            DisplayMode = mode,
            MonitorId = document.MonitorId
        };

        return OperationResult.Success($"Imported '{name}'.");
    }

    public const string GameSchema = "veyra.game/1";
    public const int MaxGameDocumentBytes = 64 * 1024;

    public static string ExportGame(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = new GameProfileDocument
        {
            Schema = GameSchema,
            Name = profile.Name,
            ExecutableName = profile.ExecutableName,
            Priority = profile.Priority,
            AutoSwitch = profile.AutoSwitch,
            AllowDisplayChanges = profile.AllowDisplayChanges,
            Notes = profile.Notes
        };

        return JsonSerializer.Serialize(document, Options);
    }

    public static OperationResult ImportGame(string json, out GameProfile? profile)
    {
        profile = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return OperationResult.Invalid("The file is empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxGameDocumentBytes)
        {
            return OperationResult.Invalid("The game profile is larger than the 64 KB limit.");
        }

        GameProfileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<GameProfileDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            return OperationResult.Invalid("The file is not a valid Veyra game profile.", ex.Message);
        }

        if (document is null)
        {
            return OperationResult.Invalid("The profile could not be read.");
        }

        if (!string.Equals(document.Schema, GameSchema, StringComparison.Ordinal))
        {
            return OperationResult.Invalid($"Unsupported game profile schema '{document.Schema}'. Expected '{GameSchema}'.");
        }

        var name = (document.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120)
        {
            return OperationResult.Invalid("The profile needs a name between 1 and 120 characters.");
        }

        var executable = (document.ExecutableName ?? string.Empty).Trim();
        if (!IsSafeExecutableName(executable))
        {
            return OperationResult.Invalid("The executable must be a plain file name ending in .exe, without folders, drive letters or other paths.");
        }

        if (document.Priority is < 0 or > 1000)
        {
            return OperationResult.Invalid("Priority must be between 0 and 1000.");
        }

        if (document.Notes is { Length: > 2000 })
        {
            return OperationResult.Invalid("Notes are limited to 2000 characters.");
        }

        profile = new GameProfile
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            Name = name,
            ExecutableName = executable,
            Priority = document.Priority,
            AutoSwitch = document.AutoSwitch,
            AllowDisplayChanges = document.AllowDisplayChanges,
            Notes = document.Notes
        };

        return OperationResult.Success($"Imported '{name}'.");
    }

    private static bool IsSafeExecutableName(string value) =>
        value.Length is > 0 and <= 260
        && value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !value.Contains(':')
        && !value.Contains('/')
        && !value.Contains((char)92);

    private static readonly Dictionary<ColourControl, (double Min, double Max)> Ranges = new()
    {
        [ColourControl.Brightness] = (-100, 100),
        [ColourControl.Contrast] = (-100, 100),
        [ColourControl.Gamma] = (0.25, 4.0),
        [ColourControl.Saturation] = (0, SaturationScale.MaximumPercent),
        [ColourControl.Hue] = (-180, 180),
        [ColourControl.Temperature] = (2000, 12000),
        [ColourControl.Red] = (-100, 100),
        [ColourControl.Green] = (-100, 100),
        [ColourControl.Blue] = (-100, 100),
        [ColourControl.Shadow] = (-100, 100),
        [ColourControl.BlackLevel] = (-50, 50)
    };
}
