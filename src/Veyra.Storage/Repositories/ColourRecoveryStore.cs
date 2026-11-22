using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;

namespace Veyra.Storage.Repositories;

/// <summary>
/// Persists the original colour values before the first change so a crash or an
/// interrupted session can restore the user's own settings on the next start.
/// </summary>
public sealed class ColourRecoveryStore : IColourRecoveryStore
{
    private readonly string _path;

    public ColourRecoveryStore(string? path = null)
    {
        _path = path ?? VeyraPaths.ColourRecovery;
        VeyraPaths.EnsureRoot();
    }

    public ColourRecoverySnapshot Load()
    {
        if (!AtomicFile.TryReadAllText(_path, out var json)) return new ColourRecoverySnapshot();

        try
        {
            return JsonSerializer.Deserialize<ColourRecoverySnapshot>(json, JsonDefaults.Options) ?? new ColourRecoverySnapshot();
        }
        catch (JsonException)
        {
            AtomicFile.Quarantine(_path);
            return new ColourRecoverySnapshot();
        }
    }

    public void Save(ColourRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(snapshot, JsonDefaults.Options));
    }

    public void MarkClean()
    {
        var current = Load();
        Save(current with { Dirty = false, CreatedAt = DateTimeOffset.UtcNow });
    }
}
