using Acut.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public class DatabaseInitializerTests
{
    [Fact]
    public void Initialize_NewDatabase_CreatesSchemaAndSeedsCatalog()
    {
        using var db = new TestDatabase();

        Assert.Equal(6, db.Catalog.GetBuildingTypes().Count);
        Assert.NotEmpty(db.Catalog.GetItems());
        Assert.NotEmpty(db.Catalog.GetRecipes());
        Assert.NotEmpty(db.Catalog.GetSkills());
        Assert.NotEmpty(db.Catalog.GetQuests());
        Assert.NotEmpty(db.Catalog.GetAchievements());
        Assert.NotEmpty(db.Catalog.GetEnemies());
        Assert.NotEmpty(db.Catalog.GetStocks());
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotDuplicateCatalog()
    {
        using var db = new TestDatabase();
        var before = db.Count("BuildingType");

        new DatabaseInitializer(db.Factory).Initialize();

        Assert.Equal(before, db.Count("BuildingType"));
    }

    [Fact]
    public void Initialize_SetsSchemaVersion()
    {
        using var db = new TestDatabase();

        using var connection = db.Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        Assert.Equal(DatabaseInitializer.CurrentSchemaVersion, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Catalog_SkillsKnowPrerequisitesAndUnlockedBuildings()
    {
        using var db = new TestDatabase();

        var skills = db.Catalog.GetSkills().ToDictionary(s => s.Id);

        Assert.Contains(skills[1], skills[2].Prerequisites);
        Assert.Single(skills[3].UnlocksBuildings);
    }

    [Fact]
    public void ForeignKeys_AreEnforced()
    {
        using var db = new TestDatabase();
        using var connection = db.Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower, Defense, SkillPoints, HairStyle, SkinColor, Outfit) " +
                              "VALUES ('нема-такого', 'x', 1, 0, 0, 1, 1, 1, 1, 0, 'a', 'b', 'c')";

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }
}
