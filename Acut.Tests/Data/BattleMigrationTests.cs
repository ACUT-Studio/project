using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class BattleMigrationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PreviousVersion_MigratesActiveBattleWithoutChangingPosition(int version)
    {
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"battle-migration-{Guid.NewGuid():N}", Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared, Pooling = false
        });
        using var anchor = factory.CreateConnection();
        foreach (var name in new[] { "schema.sql", "migration_002.sql" }
            .Concat(version == 3 ? new[] { "migration_003.sql" } : Array.Empty<string>()).Append("seed.sql"))
        {
            using var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            Execute(anchor, reader.ReadToEnd());
        }
        Execute(anchor, """
            INSERT INTO GameSave VALUES ('battle-save','Герой','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','Battle');
            INSERT INTO ActiveBattle (SaveId, EnemyId, EnemyHealth, TurnNumber, IsPlayerTurn)
                SELECT 'battle-save', EnemyId, 1, 7, 0 FROM EnemyDefinition ORDER BY EnemyId LIMIT 1;
            """);
        Execute(anchor, $"PRAGMA user_version = {version};");
        var initializer = new DatabaseInitializer(factory);
        initializer.Initialize();
        initializer.Initialize();
        using var command = anchor.CreateCommand();
        command.CommandText = "SELECT EnemyHealth, TurnNumber, IsPlayerTurn, PlayerDefending, EnemyDefending FROM ActiveBattle";
        using (var reader = command.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(7, reader.GetInt32(1));
            Assert.Equal(0, reader.GetInt32(2));
            Assert.Equal(0, reader.GetInt32(3));
            Assert.Equal(0, reader.GetInt32(4));
        }
        Assert.Throws<SqliteException>(() => Execute(anchor, "UPDATE ActiveBattle SET PlayerDefending=2"));
        Assert.Throws<SqliteException>(() => Execute(anchor, "UPDATE ActiveBattle SET EnemyDefending=-1"));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
