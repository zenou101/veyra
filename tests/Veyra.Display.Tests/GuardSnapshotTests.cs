using System;
using System.IO;
using Veyra.Core.Models;
using Veyra.Display.Recovery;
using Xunit;

namespace Veyra.Display.Tests;

public sealed class GuardSnapshotTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "veyra-guard-" + Guid.NewGuid().ToString("N") + ".json");

    private static DisplayRecoverySnapshot Snapshot() => new()
    {
        Token = "token",
        Displays = new[]
        {
            new DisplayState("display-1", "DISPLAY1", 1920, 1080, 165, 32, 0, 0, 0, 0, 0),
            new DisplayState("display-2", "DISPLAY2", 1200, 1920, 60, 32, 90, 1920, 0, 0, 0)
        }
    };

    [Fact]
    public void Snapshot_round_trips_with_exact_modes()
    {
        DisplaySnapshotStore.Save(_path, Snapshot());
        Assert.True(DisplaySnapshotStore.TryLoad(_path, out var loaded, out var error), error);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Displays.Count);
        Assert.Equal(165, loaded.Displays[0].RefreshRate);
        Assert.Equal(90, loaded.Displays[1].Orientation);
    }

    [Fact]
    public void Tampered_snapshot_is_refused()
    {
        DisplaySnapshotStore.Save(_path, Snapshot());
        var text = File.ReadAllText(_path).Replace("165", "60");
        File.WriteAllText(_path, text);

        Assert.False(DisplaySnapshotStore.TryLoad(_path, out _, out var error));
        Assert.Contains("integrity", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_snapshot_is_refused_without_guessing()
    {
        Assert.False(DisplaySnapshotStore.TryLoad(_path, out var loaded, out var error));
        Assert.Null(loaded);
        Assert.NotEmpty(error);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
