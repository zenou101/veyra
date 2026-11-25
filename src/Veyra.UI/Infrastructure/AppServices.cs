using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Veyra.Colour;
using Veyra.Core.Models;
using Veyra.Display.Monitors;
using Veyra.Overlay;
using Veyra.Storage.Database;
using Veyra.Storage.Repositories;

namespace Veyra.UI.Infrastructure;

/// <summary>Composition root. Services are constructed once and shared by the pages.</summary>
public static class AppServices
{
    public static VeyraDatabase Database { get; private set; } = null!;
    public static PresetRepository Presets { get; private set; } = null!;
    public static GameRepository Games { get; private set; } = null!;
    public static SettingsStore SettingsStore { get; private set; } = null!;
    public static HotkeyRepository Hotkeys { get; private set; } = null!;
    public static DisplayService Displays { get; } = new();
    public static ColourEngine Colour { get; private set; } = null!;
    public static OverlayService Overlay { get; private set; } = null!;

    public static AppSettings Settings { get; private set; } = new();

    public static string StartupStatus { get; private set; } = "Starting";

    public static async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Database = new VeyraDatabase();
        Database.Migrate();

        Presets = new PresetRepository(Database);
        Games = new GameRepository(Database);
        SettingsStore = new SettingsStore();
        Hotkeys = new HotkeyRepository();
        Colour = new ColourEngine(new ColourRecoveryStore());
        await Colour.InitializeAsync(cancellationToken);
        Overlay = new OverlayService();

        Settings = SettingsStore.Load();

        var recovery = await Colour.RecoverPendingAsync(cancellationToken);
        StartupStatus = recovery.Message;
    }

    public static void SaveSettings(Func<AppSettings, AppSettings> change)
    {
        Settings = change(Settings);
        SettingsStore.Save(Settings);
    }

    public static string DataFolder => Storage.VeyraPaths.Root;

    public static string LogsFolder => Path.Combine(Storage.VeyraPaths.Root, "logs");
}
