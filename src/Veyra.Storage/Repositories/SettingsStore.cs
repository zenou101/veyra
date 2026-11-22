using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Storage.Repositories;

/// <summary>
/// Settings are written atomically to JSON. A corrupt file is quarantined and defaults
/// are used, so a damaged configuration never prevents the application from starting.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? VeyraPaths.Settings;
        VeyraPaths.EnsureRoot();
    }

    public AppSettings Load()
    {
        if (!AtomicFile.TryReadAllText(_path, out var json))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options);
            if (settings is null)
            {
                AtomicFile.Quarantine(_path);
                return new AppSettings();
            }

            return SettingsValidator.Normalise(settings);
        }
        catch (JsonException)
        {
            AtomicFile.Quarantine(_path);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(SettingsValidator.Normalise(settings), JsonDefaults.Options));
    }
}
