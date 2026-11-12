using Acut.Core.Domain;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Acut.Data;
using Xunit;

namespace Acut.Tests.Data;

public sealed class SkillBuildingAccessTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    public void CatalogBuilding_RequiresLearnedSkillEvenWithEnoughResources(int buildingId, int skillId)
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        session.Player.AddResources(new(10000, 10000, 10000));
        var type = db.Catalog.GetBuildingTypes().Single(t => t.Id == buildingId);
        Assert.Equal(skillId, type.RequiredSkillId);
        var before = session.Player.Resources;
        Assert.Equal(BuildingStatus.Locked, session.City.GetAvailability(type));
        Assert.Throws<GameRuleException>(() => session.City.BuildBuilding(type));
        Assert.Equal(before, session.Player.Resources);
        Assert.Single(session.City.GetBuildings());

        session.Player.SkillTree.SkillPoints = 5;
        var skills = session.Player.SkillTree.Skills;
        if (skillId != 1) Assert.True(session.Player.SkillTree.Unlock(skills.Single(s => s.Id == 1)));
        Assert.True(session.Player.SkillTree.Unlock(skills.Single(s => s.Id == skillId)));
        Assert.Equal(BuildingStatus.Available, session.City.GetAvailability(type));
        Assert.Null(session.City.GetBuildFailureReason(type));
        Assert.Equal(buildingId, session.City.BuildBuilding(type).TypeId);
        Assert.Equal(before, session.Player.Resources.Add(type.BuildResources));
    }

    [Fact]
    public void VersionFour_MigratesSkillRequirementsAndPreservesSavedCity()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        session.Player.AddResources(new(500, 1000, 1000));
        Assert.True(session.Player.SkillTree.Unlock(session.Player.SkillTree.Skills.Single(s => s.Id == 1)));
        var house = session.City.BuildBuilding(db.Catalog.GetBuildingTypes().Single(t => t.Id == 2));
        db.Saves.Save(session);
        using (var connection = db.Factory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE BuildingType SET RequiredSkillId = NULL; PRAGMA user_version = 4;";
            command.ExecuteNonQuery();
        }
        var initializer = new DatabaseInitializer(db.Factory);
        initializer.Initialize();
        initializer.Initialize();
        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(session.Player.Resources, loaded.Player.Resources);
        Assert.Equal(session.Player.SkillPoints, loaded.Player.SkillPoints);
        Assert.True(loaded.Player.SkillTree.IsUnlocked(1));
        Assert.Equal(house.InvestedResources, loaded.City.GetBuildings().Single(b => b.Id == house.Id).InvestedResources);
        Assert.Equal(2, loaded.City.GetBuildings().Count);
        var types = db.Catalog.GetBuildingTypes().ToDictionary(t => t.Id);
        Assert.Equal(1, types[2].RequiredSkillId);
        Assert.Equal(1, types[4].RequiredSkillId);
        Assert.Equal(2, types[5].RequiredSkillId);
        Assert.Equal(3, types[6].RequiredSkillId);
        Assert.Equal(BuildingStatus.Locked, loaded.City.GetAvailability(types[5]));
        Assert.Equal(BuildingStatus.Locked, loaded.City.GetAvailability(types[6]));
    }
}
