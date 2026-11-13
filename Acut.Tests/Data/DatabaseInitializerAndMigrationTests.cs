using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class DatabaseInitializerAndMigrationTests
{
    [Fact]
    public void Initialize_IsIdempotent_SetsCurrentVersionAndDoesNotDuplicateCatalog()
    {
        using var db = new TestDatabase();

        // Повторний виклик не падає і не дублює рядки каталогу
        db.Initializer.Initialize();

        using var connection = db.Factory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.Equal(DatabaseInitializer.CurrentSchemaVersion, version);
        Assert.Equal(6, db.Catalog.GetBuildingTypes().Count);
        Assert.Equal(7, db.Catalog.GetItems().Count);
        Assert.Equal(4, db.Catalog.GetEnemies().Count);
    }

    [Fact]
    public void Initialize_WhenDatabaseVersionIsNewerThanSupported_ThrowsInvalidOperationException()
    {
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"future-schema-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        });
        using var anchor = factory.CreateConnection();
        using (var cmd = anchor.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA user_version = {DatabaseInitializer.CurrentSchemaVersion + 1};";
            cmd.ExecuteNonQuery();
        }

        var initializer = new DatabaseInitializer(factory);
        Assert.Throws<InvalidOperationException>(() => initializer.Initialize());
    }

    [Fact]
    public void MigrateFromV1_DeduplicatesSpecialBuildingsRefundsGoldAndAddsFreeSawmill()
    {
        var saveId = Guid.NewGuid();
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"v1-migration-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        });

        using var anchor = factory.CreateConnection();
        using (var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream("schema.sql")!)
        using (var reader = new StreamReader(stream))
        {
            Execute(anchor, reader.ReadToEnd());
        }

        // Наповнюємо мінімальний каталог версії 1 і місто з двома майстернями
        Execute(anchor, $"""
            INSERT INTO BuildingType (BuildingTypeId, Kind, Name, BuildCost, UpgradeCost, MaxLevel, IncomePerTick, Capacity, Resource, ProductionRate, RequiredSkillId) VALUES
                (3, 'Production', 'Лісопил', 0, 0, 3, 0, NULL, 'Wood', 4, NULL),
                (5, 'Workshop', 'Майстерня зілля', 200, 100, 3, 0, NULL, NULL, NULL, NULL);

            INSERT INTO GameSave (SaveId, Name, CreatedAt, UpdatedAt, CurrentState)
            VALUES ('{saveId}', 'Старе збереження', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z', 'City');

            INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower, Defense, SkillPoints, HairStyle, SkinColor, Outfit)
            VALUES ('{saveId}', 'Мер', 1, 0, 100, 100, 100, 10, 2, 1, 'Short', 'Light', 'Traveler');

            INSERT INTO City (CityId, SaveId, Name, CityLevel, Population)
            VALUES (1, '{saveId}', 'Місто', 1, 0);

            INSERT INTO CityBuilding (BuildingId, CityId, BuildingTypeId, Level, Status) VALUES
                ('{Guid.NewGuid()}', 1, 5, 1, 'Active'),
                ('{Guid.NewGuid()}', 1, 5, 1, 'Active');

            PRAGMA user_version = 1;
            """);

        new DatabaseInitializer(factory).Initialize();

        var catalog = new SqliteCatalogRepository(factory);
        var saves = new SqliteSaveRepository(factory, catalog);
        var loaded = saves.Load(saveId)!;

        // Зайва майстерня видалена з поверненням 50% (100 золота), додано безкоштовний лісопил
        Assert.Equal(200, loaded.Player.Gold);
        Assert.Equal(150, loaded.Player.Wood);
        Assert.Single(loaded.City.GetBuildings(), b => b.TypeId == 5);
        Assert.Single(loaded.City.GetBuildings(), b => b.TypeId == 3);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}