using System.Collections.Generic;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Xunit;

namespace Veyra.Core.Tests;

public sealed class ProfileAndHotkeyTests
{
    private static GameProfile Game(string id, string exe, int priority, bool auto = true) => new()
    {
        Id = id,
        Name = id,
        ExecutableName = exe,
        Priority = priority,
        AutoSwitch = auto
    };

    [Fact]
    public void Resolver_prefers_higher_priority_when_two_games_run()
    {
        var profiles = new[] { Game("low", "a.exe", 10), Game("high", "b.exe", 200) };
        var selection = ProfileResolver.ResolveForProcesses(profiles, new[] { "a.exe", "b.exe" });
        Assert.Equal("high", selection.Active?.Id);
    }

    [Fact]
    public void Resolver_ignores_auto_switch_disabled()
    {
        var profiles = new[] { Game("off", "a.exe", 500, auto: false), Game("on", "b.exe", 10) };
        var selection = ProfileResolver.ResolveForProcesses(profiles, new[] { "a.exe", "b.exe" });
        Assert.Equal("on", selection.Active?.Id);
    }

    [Fact]
    public void Resolver_returns_none_when_nothing_runs()
    {
        var selection = ProfileResolver.ResolveForProcesses(new[] { Game("a", "a.exe", 1) }, new List<string>());
        Assert.Null(selection.Active);
    }

    [Fact]
    public void Matcher_normalises_executable_names()
    {
        var game = new GameProfile { Id = "x", Name = "Game", ExecutableName = "game.exe" };
        Assert.True(GameMatcher.Matches(game, "Game"));
        Assert.False(GameMatcher.Matches(game, "other"));
        Assert.True(GameMatcher.Matches(game, "GAME"));
    }

    [Theory]
    [InlineData("Ctrl+Alt+V", true)]
    [InlineData("Shift+F10", true)]
    [InlineData("V", false)]
    [InlineData("Ctrl+Nope", false)]
    public void Hotkey_parser_accepts_and_rejects_expected_combinations(string text, bool expected)
    {
        var ok = HotkeyCatalog.TryParse(text, out var spec, out _);
        Assert.Equal(expected, ok);
        if (ok) Assert.False(spec.IsEmpty);
    }

    [Fact]
    public void Hotkey_format_round_trips()
    {
        Assert.True(HotkeyCatalog.TryParse("Ctrl+Alt+X", out var spec, out _));
        Assert.Equal("Ctrl+Alt+X", HotkeyCatalog.Format(spec));
    }

    [Fact]
    public void Duplicate_hotkeys_are_reported()
    {
        var bindings = new[]
        {
            new HotkeyBinding { Action = HotkeyAction.OpenVeyra, Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4F) },
            new HotkeyBinding { Action = HotkeyAction.ToggleCrosshair, Spec = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4F) }
        };

        Assert.Single(HotkeyCatalog.FindConflicts(bindings));
    }

    [Fact]
    public void Ramp_calculator_moves_output_when_brightness_changes()
    {
        var neutral = ColourRampCalculator.BuildRamp(ColourSettings.Neutral);
        var dim = ColourRampCalculator.BuildRamp(new ColourSettings { Brightness = -40 });
        Assert.True(dim[128] < neutral[128]);
    }
}
