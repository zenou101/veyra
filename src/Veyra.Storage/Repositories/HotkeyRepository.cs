using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Core.Services;

namespace Veyra.Storage.Repositories;

public sealed class HotkeyRepository : IHotkeyRepository
{
    private readonly string _path;

    public HotkeyRepository(string? path = null)
    {
        _path = path ?? VeyraPaths.Hotkeys;
        VeyraPaths.EnsureRoot();
    }

    public IReadOnlyList<HotkeyBinding> GetAll()
    {
        if (!AtomicFile.TryReadAllText(_path, out var json)) return HotkeyDefaults.Create();

        try
        {
            var bindings = JsonSerializer.Deserialize<List<HotkeyBinding>>(json, JsonDefaults.Options);
            return bindings is { Count: > 0 } ? bindings : HotkeyDefaults.Create();
        }
        catch (JsonException)
        {
            AtomicFile.Quarantine(_path);
            return HotkeyDefaults.Create();
        }
    }

    public void Save(IReadOnlyList<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(bindings, JsonDefaults.Options));
    }
}
