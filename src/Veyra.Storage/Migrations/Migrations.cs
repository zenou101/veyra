namespace Veyra.Storage.Migrations;

public sealed record Migration(long Version, string[] Statements);

public static class MigrationCatalog
{
    public static IReadOnlyList<Migration> All { get; } = new[]
    {
        new Migration(1, new[]
        {
            """
            CREATE TABLE IF NOT EXISTS presets (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                kind INTEGER NOT NULL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY,
                executable_name TEXT NOT NULL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """
        }),
        new Migration(2, new[]
        {
            "CREATE INDEX IF NOT EXISTS ix_games_executable ON games (executable_name);",
            "CREATE INDEX IF NOT EXISTS ix_presets_kind ON presets (kind);"
        })
    };
}
