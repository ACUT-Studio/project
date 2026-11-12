using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class ResourcePersistenceTests
{
    [Fact]
    public void NewGame_HasFreeSawmillAndExactlyEnoughWoodForMine()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        var sawmill = Assert.Single(session.City.GetBuildings());
        Assert.Equal(3, sawmill.TypeId);
        Assert.True(sawmill.InvestedResources.IsEmpty);
        var mineType = db.Catalog.GetBuildingTypes().Single(t => t.Id == 4);
        Assert.Equal(mineType.BuildResources, session.Player.Resources);
        Assert.True(session.Player.SkillTree.Unlock(session.Player.SkillTree.Skills.Single(s => s.Id == 1)));
        session.City.BuildBuilding(mineType);
        Assert.True(session.Player.Resources.IsEmpty);
        session.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(new ResourceAmounts(wood: 4, stone: 4), session.Player.Resources);
        Assert.Equal(0, session.Player.Statistics.TotalGoldEarned);
    }

    [Fact]
    public void Catalog_CostsProductionAndUniqueMechanicsMatchResourceRules()
    {
        using var db = new TestDatabase();
        var types = db.Catalog.GetBuildingTypes().ToDictionary(t => t.Id);
        foreach (var id in new[] { 1, 4 })
        {
            Assert.Equal(0, types[id].BuildCost);
            Assert.Equal(0, types[id].BuildStoneCost);
            Assert.True(types[id].BuildWoodCost > 0);
        }
        foreach (var id in new[] { 2, 5, 6 })
        {
            Assert.True(types[id].BuildCost > 0);
            Assert.True(types[id].BuildWoodCost > 0);
            Assert.True(types[id].BuildStoneCost > 0);
        }
        Assert.Equal("Wood", types[3].Resource);
        Assert.Equal("Stone", types[4].Resource);
        Assert.Null(types[1].RequiredSkillId);
        Assert.Null(types[3].RequiredSkillId);
        Assert.Equal(1, types[2].RequiredSkillId);
        Assert.Equal(1, types[4].RequiredSkillId);
        Assert.Equal(2, types[5].RequiredSkillId);
        Assert.Equal(3, types[6].RequiredSkillId);
        Assert.All(new[] { types[5], types[6] }, t => Assert.True(BuildingFactory.Create(t).OutputPerTick.IsEmpty));
    }

    [Fact]
    public void SaveLoad_PreservesAllResourcesFreeBuildingAndPaidInvestments()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        new SaveManager(db.Saves).Register(session);
        session.Player.AddResources(new(500, 1000, 1000));
        session.Player.SkillTree.SkillPoints = 5;
        foreach (var skill in session.Player.SkillTree.Skills.OrderBy(s => s.Id))
            Assert.True(session.Player.SkillTree.Unlock(skill));
        var types = db.Catalog.GetBuildingTypes().ToDictionary(t => t.Id);
        var house = session.City.BuildBuilding(types[2]);
        session.City.UpgradeBuilding(house);
        session.City.BuildBuilding(types[5]);
        session.City.BuildBuilding(types[6]);
        var expected = session.Player.Resources;
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(expected, loaded.Player.Resources);
        Assert.True(loaded.City.HasMarket);
        Assert.True(loaded.City.HasWorkshop);
        var freeSawmill = loaded.City.GetBuildings().Single(b => b.TypeId == 3);
        Assert.True(freeSawmill.InvestedResources.IsEmpty);
        var loadedHouse = loaded.City.GetBuildings().Single(b => b.Id == house.Id);
        Assert.Equal(house.InvestedResources, loadedHouse.InvestedResources);
        Assert.Equal(house.DemolitionResources, loadedHouse.DemolitionResources);
        loaded.City.RemoveBuilding(loadedHouse);
        var afterSale = expected.Add(house.DemolitionResources);
        Assert.Equal(afterSale, loaded.Player.Resources);
        db.Saves.Save(loaded);
        var reloaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(afterSale, reloaded.Player.Resources);
        Assert.DoesNotContain(reloaded.City.GetBuildings(), b => b.Id == house.Id);
        Assert.True(reloaded.City.GetBuildings().Single(b => b.TypeId == 3).DemolitionResources.IsEmpty);
    }

    [Fact]
    public void FailedSave_RollsBackResourceBalancesAndBuildingsTogether()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        new SaveManager(db.Saves).Register(session);
        session.Save();
        var before = session.Player.Resources;
        session.Player.AddResources(new(500, 500, 500));
        session.City.BuildBuilding(db.Catalog.GetBuildingTypes().Single(t => t.Id == 1));
        session.Player.Inventory.AddItem(new Weapon(9999, "Тест", "", 0, 1));
        Assert.Throws<SqliteException>(() => session.Save());
        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(before, loaded.Player.Resources);
        Assert.Single(loaded.City.GetBuildings());
        Assert.Empty(loaded.Player.Inventory.GetItems());
    }
}
