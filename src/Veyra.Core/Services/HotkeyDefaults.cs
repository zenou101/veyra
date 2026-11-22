using Veyra.Core.Models;

namespace Veyra.Core.Services;

/// <summary>
/// Default global hotkeys. Ctrl+Alt combinations are used deliberately so they do not
/// collide with common application and Windows shortcuts.
/// </summary>
public static class HotkeyDefaults
{
    public static IReadOnlyList<HotkeyBinding> Create() => new[]
    {
        new HotkeyBinding
        {
            Action = HotkeyAction.ToggleColourEnhancements,
            Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x56)
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.NextPreset,
            Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x50)
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.RestoreDefaults,
            Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x52)
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.ToggleCrosshair,
            Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x58)
        },
        new HotkeyBinding
        {
            Action = HotkeyAction.OpenVeyra,
            Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4F)
        }
    };
}
