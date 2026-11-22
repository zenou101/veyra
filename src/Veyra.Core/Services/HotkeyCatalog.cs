using Veyra.Core.Models;

namespace Veyra.Core.Services;

/// <summary>Parses, formats and conflict-checks global hotkeys.</summary>
public static class HotkeyCatalog
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Num0"] = 0x60,
        ["Num1"] = 0x61,
        ["Num2"] = 0x62,
        ["Num3"] = 0x63,
        ["Num4"] = 0x64,
        ["Num5"] = 0x65,
        ["Num6"] = 0x66,
        ["Num7"] = 0x67,
        ["Num8"] = 0x68,
        ["Num9"] = 0x69,
        ["Semicolon"] = 0xBA,
        ["Equals"] = 0xBB,
        ["Comma"] = 0xBC,
        ["Minus"] = 0xBD,
        ["Period"] = 0xBE,
        ["Slash"] = 0xBF,
        ["Tilde"] = 0xC0,
        ["BracketLeft"] = 0xDB,
        ["Backslash"] = 0xDC,
        ["BracketRight"] = 0xDD,
        ["Quote"] = 0xDE
    };

    public static bool TryParse(string? text, out HotkeySpec spec, out string error)
    {
        spec = HotkeySpec.None;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter a key combination, for example Ctrl+Shift+V.";
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "Enter a key combination, for example Ctrl+Shift+V.";
            return false;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;

            if (!isLast)
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= HotkeyModifiers.Control;
                        break;
                    case "alt":
                        modifiers |= HotkeyModifiers.Alt;
                        break;
                    case "shift":
                        modifiers |= HotkeyModifiers.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= HotkeyModifiers.Win;
                        break;
                    default:
                        error = $"'{part}' is not a modifier. Use Ctrl, Alt, Shift or Win.";
                        return false;
                }

                continue;
            }

            if (!TryParseKey(part, out var key))
            {
                error = $"'{part}' is not a supported key.";
                return false;
            }

            spec = new HotkeySpec(modifiers, key);
        }

        if (spec.IsEmpty)
        {
            error = "Choose a key for the combination.";
            return false;
        }

        if (modifiers == HotkeyModifiers.None && spec.VirtualKey is >= 0x30 and <= 0x5A)
        {
            error = "A letter or number needs a modifier so it does not interfere with typing.";
            return false;
        }

        if (IsReserved(spec))
        {
            error = "That combination is reserved by Windows.";
            spec = HotkeySpec.None;
            return false;
        }

        return true;
    }

    private static bool TryParseKey(string part, out int key)
    {
        key = 0;
        if (part.Length == 1)
        {
            var c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                key = c;
                return true;
            }
        }

        if (part.Length is >= 2 and <= 3 && (part[0] is 'F' or 'f') && int.TryParse(part[1..], out var fn) && fn is >= 1 and <= 24)
        {
            key = 0x70 + (fn - 1);
            return true;
        }

        if (NamedKeys.TryGetValue(part, out var named))
        {
            key = named;
            return true;
        }

        return false;
    }

    public static string Format(HotkeySpec spec)
    {
        if (spec.IsEmpty) return "Not set";

        var parts = new List<string>();
        if (spec.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (spec.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (spec.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (spec.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(FormatKey(spec.VirtualKey));
        return string.Join("+", parts);
    }

    private static string FormatKey(int key)
    {
        if (key is >= 0x30 and <= 0x5A) return ((char)key).ToString();
        if (key is >= 0x70 and <= 0x87) return "F" + (key - 0x70 + 1);
        foreach (var pair in NamedKeys)
        {
            if (pair.Value == key) return pair.Key;
        }
        return "0x" + key.ToString("X2");
    }

    public static bool IsReserved(HotkeySpec spec) =>
        spec is { Modifiers: HotkeyModifiers.Win, VirtualKey: 0x4C } ||
        spec is { Modifiers: HotkeyModifiers.Control or HotkeyModifiers.Alt, VirtualKey: 0x2E };

    public static IReadOnlyList<string> FindConflicts(IEnumerable<HotkeyBinding> bindings)
    {
        var conflicts = new List<string>();
        var used = new Dictionary<(HotkeyModifiers, int), HotkeyAction>();

        foreach (var binding in bindings.Where(b => b.Enabled && !b.Spec.IsEmpty))
        {
            var key = (binding.Spec.Modifiers, binding.Spec.VirtualKey);
            if (used.TryGetValue(key, out var existing))
            {
                conflicts.Add($"{Format(binding.Spec)} is assigned to both {existing} and {binding.Action}.");
            }
            else
            {
                used[key] = binding.Action;
            }
        }

        return conflicts;
    }

}
