using System.Text.Json;
using Veyra.Core;
using Veyra.Core.Models;
using Veyra.Storage.Database;

namespace Veyra.Storage.Repositories;

public sealed class GameRepository : IGameRepository
{
    private readonly VeyraDatabase _database;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GameRepository(VeyraDatabase database)
    {
        _database = database;
        _database.Migrate();
    }

    public Task<IReadOnlyList<GameProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<GameProfile>>(() =>
        {
            var result = new List<GameProfile>();
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM games ORDER BY executable_name COLLATE NOCASE;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                try
                {
                    var profile = JsonSerializer.Deserialize<GameProfile>(reader.GetString(0), JsonDefaults.Options);
                    if (profile is not null) result.Add(profile);
                }
                catch (JsonException)
                {
                    // A damaged row is skipped rather than faulting the whole library.
                }
            }

            return result;
        }, cancellationToken);

    public async Task SaveAsync(GameProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO games (id, executable_name, json, updated_at)
                VALUES ($id, $exe, $json, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    executable_name = excluded.executable_name,
                    json = excluded.json,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$id", profile.Id);
            command.Parameters.AddWithValue("$exe", profile.ExecutableName);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(profile, JsonDefaults.Options));
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
            command.CommandText = "DELETE FROM games WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        finally
        {
            _gate.Release();
        }
    }
}
