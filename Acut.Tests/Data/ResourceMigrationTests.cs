using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class ResourceMigrationTests
{
    [Fact]
    public void VersionOne_MigratesBalancesInvestmentsAndDuplicateSpecialBuildingsOnce()
    {
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"migration-{Guid.NewGuid():N}", Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared, Pooling = false
        });
        using var anchor = factory.CreateConnection();
        using (var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream("schema.sql")!)
        using (var reader = new StreamReader(stream))
            Execute(anchor, reader.ReadToEnd());
        Execute(anchor, """
            INSERT INTO BuildingType (BuildingTypeId, Kind, Name, BuildCost, UpgradeCost, MaxLevel, IncomePerTick, Capacity, Resource, ProductionRate) VALUES
              (1,'Residential','Хатина',100,80,3,2,4,NULL,NULL),
              (2,'Residential','Кам''яний будинок',250,200,3,5,10,NULL,NULL),
              (3,'Production','Ферма',150,120,3,4,NULL,'Food',1),
              (4,'Production','Шахта',400,300,3,10,NULL,'Ore',1),
              (5,'Workshop','Майстерня зілля',600,450,3,0,NULL,NULL,NULL),
              (6,'Market','Ринок',800,600,3,6,NULL,NULL,NULL);
            INSERT INTO GameSave VALUES ('00000000-0000-0000-0000-000000000001','Тест','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','City');
            INSERT INTO Player (SaveId,Name,Level,Experience,Gold,Health,MaxHealth,AttackPower,Defense,SkillPoints,HairStyle,SkinColor,Outfit)
              VALUES ('00000000-0000-0000-0000-000000000001','Тест',1,0,100,100,100,10,2,1,'Short','Light','Traveler');
            INSERT INTO City (SaveId,Name,CityLevel,Population) VALUES ('00000000-0000-0000-0000-000000000001','Місто',1,8);
            INSERT INTO CityBuilding VALUES
              ('00000000-0000-0000-0000-000000000011',1,1,2,'Active'),
              ('00000000-0000-0000-0000-000000000012',1,5,2,'Active'),
              ('00000000-0000-0000-0000-000000000013',1,5,2,'Active'),
              ('00000000-0000-0000-0000-000000000014',1,6,1,'Active'),
              ('00000000-0000-0000-0000-000000000015',1,6,1,'Active');
            PRAGMA user_version = 1;
            """);

        var initializer = new DatabaseInitializer(factory);
        initializer.Initialize();
        Assert.Equal(2L, Scalar(anchor, "PRAGMA user_version"));
        Assert.Equal(1025L, Scalar(anchor, "SELECT Gold FROM Player"));
        Assert.Equal(150L, Scalar(anchor, "SELECT Wood FROM Player"));
        Assert.Equal(0L, Scalar(anchor, "SELECT Stone FROM Player"));
        Assert.Equal(180L, Scalar(anchor, "SELECT SpentGold FROM CityBuilding WHERE BuildingTypeId=1"));
        Assert.Equal(1L, Scalar(anchor, "SELECT COUNT(*) FROM CityBuilding WHERE BuildingTypeId=5"));
        Assert.Equal(1L, Scalar(anchor, "SELECT COUNT(*) FROM CityBuilding WHERE BuildingTypeId=6"));
        Assert.Equal(0L, Scalar(anchor, "SELECT SpentWood FROM CityBuilding WHERE BuildingTypeId=3"));
        Assert.Equal("Лісопил", Scalar(anchor, "SELECT Name FROM BuildingType WHERE BuildingTypeId=3"));
        Assert.Equal("Stone", Scalar(anchor, "SELECT Resource FROM BuildingType WHERE BuildingTypeId=4"));
        initializer.Initialize();
        Assert.Equal(1025L, Scalar(anchor, "SELECT Gold FROM Player"));
        Assert.Equal(1L, Scalar(anchor, "SELECT COUNT(*) FROM CityBuilding WHERE BuildingTypeId=3"));
        Assert.Throws<SqliteException>(() => Execute(anchor, "UPDATE Player SET Wood=-1"));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
