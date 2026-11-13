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
        var saveId = Guid.NewGuid();
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"battle-migration-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        });

        using var anchor = factory.CreateConnection();
        foreach (var name in new[] { "schema.sql", "migration_002.sql" }
            .Concat(version == 3 ? new[] { "migration_003.sql" } : Array.Empty<string>())
            .Append("seed.sql"))
        {
            using var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            Execute(anchor, reader.ReadToEnd());
        }

        Execute(anchor, $"""
            INSERT INTO GameSave (SaveId, Name, CreatedAt, UpdatedAt, CurrentState)
            VALUES ('{saveId}', 'Герой (рівень 1)', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z', 'Battle');

            INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower, Defense, SkillPoints, HairStyle, SkinColor, Outfit, Wood, Stone)
            VALUES ('{saveId}', 'Герой', 1, 0, 100, 80, 100, 10, 2, 1, 'Short', 'Light', 'Traveler', 150, 0);

            INSERT INTO City (SaveId, Name, CityLevel, Population)
            VALUES ('{saveId}', 'Місто Герой', 1, 0);

            INSERT INTO ActiveBattle (SaveId, EnemyId, EnemyHealth, TurnNumber, IsPlayerTurn)
            VALUES ('{saveId}', 1, 18, 3, 0);

            PRAGMA user_version = {version};
            """);

        new DatabaseInitializer(factory).Initialize();

        var catalog = new SqliteCatalogRepository(factory);
        var saves = new SqliteSaveRepository(factory, catalog);
        var loaded = saves.Load(saveId);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.ActiveBattle);
        Assert.Equal(18, loaded.ActiveBattle.Enemy.Health);
        Assert.Equal(3, loaded.ActiveBattle.TurnNumber);
        Assert.False(loaded.ActiveBattle.IsPlayerTurn);
        Assert.False(loaded.ActiveBattle.PlayerDefending);
        Assert.False(loaded.ActiveBattle.EnemyDefending);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}