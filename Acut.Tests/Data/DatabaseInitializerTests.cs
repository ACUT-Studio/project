using Acut.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public class DatabaseInitializerTests
{
   
    [Fact]
    public void Initialize_NewDatabase_CreatesSchemaAndSeedsCatalog()
    {
        // Arrange & Act
        using var db = new TestDatabase();

        // Assert
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
        // Arrange
        using var db = new TestDatabase();
        var before = db.Count("BuildingType");

        // Act
        new DatabaseInitializer(db.Factory).Initialize();

        // Assert
        Assert.Equal(before, db.Count("BuildingType"));
    }

    [Fact]
    public void Initialize_SetsSchemaVersion()
    {
        // Arrange
        using var db = new TestDatabase();

        // Act
        using var connection = db.Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var schemaVersion = Convert.ToInt32(command.ExecuteScalar());

        // Assert
        Assert.Equal(DatabaseInitializer.CurrentSchemaVersion, schemaVersion);
    }

    [Fact]
    public void Catalog_SkillsKnowPrerequisitesAndUnlockedBuildings()
    {
        // Arrange
        using var db = new TestDatabase();
        var skills = db.Catalog.GetSkills().ToDictionary(s => s.Id);

        // Act & Assert
        Assert.Contains(skills[1], skills[2].Prerequisites);
        Assert.Single(skills[3].UnlocksBuildings);
    }

    [Fact]
    public void ForeignKeys_AreEnforced()
    {
        // Arrange
        using var db = new TestDatabase();
        using var connection = db.Factory.CreateConnection();
        using var command = connection.CreateCommand();

        // Act
        command.CommandText = "INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower, Defense, SkillPoints, HairStyle, SkinColor, Outfit) " +
                              "VALUES ('нема-такого', 'x', 1, 0, 0, 1, 1, 1, 1, 0, 'a', 'b', 'c')";

        // Assert
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }


    [Fact]
    public void Catalog_BuildingTypesHaveValidProperties()
    {
        // Arrange
        using var db = new TestDatabase();

        // Act
        var buildingTypes = db.Catalog.GetBuildingTypes();
       
        // Assert
        foreach (var building in buildingTypes)
        {
            Assert.False(string.IsNullOrWhiteSpace(building.Name));
            Assert.True(building.BuildCost >= 0);
        }
    }

    [Fact]
    public void Database_ConnectionOpensSuccessfully()
    {
        // Arrange
        using var db = new TestDatabase();

        // Act
        using var connection = db.Factory.CreateConnection();
        connection.Open();

        // Assert
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void Catalog_QuestsHaveValidRewardsAndConditions()
    {
        // Arrange
        using var db = new TestDatabase();

        // Act
        var quests = db.Catalog.GetQuests();

        // Assert
        Assert.All(quests, quest => Assert.False(string.IsNullOrWhiteSpace(quest.Title)));
    }

   
}