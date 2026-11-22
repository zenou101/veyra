using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Veyra.Core.Models;

namespace Veyra.UI.Infrastructure;

/// <summary>Registers global hotkeys on the main window's message loop.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly HwndSource? _source;
    private readonly Dictionary<int, HotkeyBinding> _registered = new();
    private int _nextId = 0x100;

    public HotkeyService(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(Hook);
    }

    public event EventHandler<HotkeyAction>? Pressed;

    public void Register(IEnumerable<HotkeyBinding> bindings)
    {
        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_source?.Handle ?? IntPtr.Zero, id);
        }

        _registered.Clear();

        foreach (var binding in bindings)
        {
            if (!binding.Enabled || binding.Spec.IsEmpty) continue;

            var id = _nextId++;
            if (RegisterHotKey(_source?.Handle ?? IntPtr.Zero, id, (uint)binding.Spec.Modifiers, (uint)binding.Spec.VirtualKey))
            {
                _registered[id] = binding;
            }
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var binding))
        {
            Pressed?.Invoke(this, binding.Action);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered.Keys) UnregisterHotKey(_source?.Handle ?? IntPtr.Zero, id);
        _registered.Clear();
        _source?.RemoveHook(Hook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
