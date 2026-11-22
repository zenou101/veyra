using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Veyra.Core.Models;
using Veyra.Storage.Database;
using Veyra.Storage.Repositories;
using Xunit;

namespace Veyra.Storage.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "veyra-tests-" + Guid.NewGuid().ToString("N"));

    private VeyraDatabase Database()
    {
        Directory.CreateDirectory(_root);
        var db = new VeyraDatabase(Path.Combine(_root, "test.db"));
        db.Migrate();
        return db;
    }

    [Fact]
    public void Migration_creates_database_and_is_idempotent()
    {
        var db = Database();
        db.Migrate();
        using var connection = db.Open();
        Assert.True(File.Exists(db.DatabasePath));
    }

    [Fact]
    public async Task Presets_round_trip_and_delete()
    {
        var repo = new PresetRepository(Database());
        await repo.SaveAsync(new Preset { Id = "p1", Name = "Test", Colour = new ColourSettings { Gamma = 1.2 } });

        var loaded = await repo.GetAsync("p1");
        Assert.NotNull(loaded);
        Assert.Equal(1.2, loaded!.Colour.Gamma);

        await repo.DeleteAsync("p1");
        Assert.Null(await repo.GetAsync("p1"));
    }

    [Fact]
    public async Task Games_priority_is_preserved()
    {
        var repo = new GameRepository(Database());
        await repo.SaveAsync(new GameProfile { Id = "g1", Name = "One", ExecutableName = "one.exe", Priority = 5 });
        await repo.SaveAsync(new GameProfile { Id = "g2", Name = "Two", ExecutableName = "two.exe", Priority = 50 });

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("g2", all.OrderByDescending(g => g.Priority).First().Id);
    }

    [Fact]
    public void Settings_store_clamps_invalid_values()
    {
        var store = new SettingsStore(Path.Combine(_root, "settings.json"));
        store.Save(new AppSettings { WindowWidth = 100, WindowHeight = 99999, ConfirmationTimeoutSeconds = 1 });

        var loaded = store.Load();
        Assert.True(loaded.WindowWidth >= 900);
        Assert.True(loaded.WindowHeight <= 4000);
        Assert.True(loaded.ConfirmationTimeoutSeconds >= 5);
    }

    [Fact]
    public void Hotkeys_fall_back_to_defaults_when_file_is_missing()
    {
        var repo = new HotkeyRepository(Path.Combine(_root, "hotkeys.json"));
        Assert.NotEmpty(repo.GetAll());
    }

    [Fact]
    public void Colour_recovery_snapshot_round_trips_and_marks_clean()
    {
        var store = new ColourRecoveryStore(Path.Combine(_root, "recovery.json"));
        store.Save(new ColourRecoverySnapshot
        {
            Dirty = true,
            Entries = new[] { new ColourRecoveryEntry("display-1", "Windows GDI gamma ramp", "abc", new System.Collections.Generic.Dictionary<int, int>(), DateTimeOffset.UtcNow) }
        });

        Assert.True(store.Load().Dirty);
        store.MarkClean();
        Assert.False(store.Load().Dirty);
    }

    [Fact]
    public void Atomic_write_replaces_without_leaving_temporary_files()
    {
        var path = Path.Combine(_root, "atomic.txt");
        AtomicFile.WriteAllText(path, "one");
        AtomicFile.WriteAllText(path, "two");
        Assert.Equal("two", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
