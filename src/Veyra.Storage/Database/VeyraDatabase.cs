using Microsoft.Data.Sqlite;
using Veyra.Storage.Migrations;

namespace Veyra.Storage.Database;

/// <summary>
/// SQLite storage with forward-only, versioned migrations. The schema version is
/// recorded in the database so an older build refuses rather than corrupting data.
/// </summary>
public sealed class VeyraDatabase
{
    private readonly string _connectionString;

    public VeyraDatabase(string? databasePath = null)
    {
        DatabasePath = databasePath ?? VeyraPaths.Database;
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Migrate()
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);");

        long current;
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
            current = Convert.ToInt64(read.ExecuteScalar() ?? 0L);
        }

        foreach (var migration in MigrationCatalog.All.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            foreach (var statement in migration.Statements)
            {
                Execute(connection, transaction, statement);
            }

            using var record = connection.CreateCommand();
            record.Transaction = transaction;
            record.CommandText = "INSERT INTO schema_version (version) VALUES ($v);";
            record.Parameters.AddWithValue("$v", migration.Version);
            record.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
