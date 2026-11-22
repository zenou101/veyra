using System.Text.Json;
using Microsoft.Data.Sqlite;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Core.Services;
using Veyra.Storage.Database;

namespace Veyra.Storage.Repositories;

public sealed class PresetRepository : IPresetRepository
{
    private readonly VeyraDatabase _database;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PresetRepository(VeyraDatabase database)
    {
        _database = database;
        _database.Migrate();
        SeedBuiltIns();
    }

    private void SeedBuiltIns()
    {
        using var connection = _database.Open();
        foreach (var preset in BuiltInPresets.Create())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO presets (id, name, kind, json, updated_at)
                VALUES ($id, $name, $kind, $json, $updated);
                """;
            command.Parameters.AddWithValue("$id", preset.Id);
            command.Parameters.AddWithValue("$name", preset.Name);
            command.Parameters.AddWithValue("$kind", (int)preset.Kind);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(preset, JsonDefaults.Options));
            command.Parameters.AddWithValue("$updated", preset.UpdatedAt.ToString("O"));
            command.ExecuteNonQuery();
        }
    }

    public Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<Preset>>(() =>
        {
            var result = new List<Preset>();
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM presets ORDER BY name COLLATE NOCASE;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var preset = Deserialize(reader.GetString(0));
                if (preset is not null) result.Add(preset);
            }

            return result;
        }, cancellationToken);

    public Task<Preset?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM presets WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            var json = command.ExecuteScalar() as string;
            return json is null ? null : Deserialize(json);
        }, cancellationToken);

    public async Task SaveAsync(Preset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO presets (id, name, kind, json, updated_at)
                VALUES ($id, $name, $kind, $json, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    kind = excluded.kind,
                    json = excluded.json,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$id", preset.Id);
            command.Parameters.AddWithValue("$name", preset.Name);
            command.Parameters.AddWithValue("$kind", (int)preset.Kind);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(preset, JsonDefaults.Options));
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM presets WHERE id = $id AND id NOT LIKE 'builtin.%';";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Preset? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Preset>(json, JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
