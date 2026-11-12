using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class CityBuildingManagementTests
{
    private static (City City, Player Player) Create()
    {
        var player = new Player("Тест", AppearanceSettings.Default, gold: 10000);
        var dispatcher = new GameEventDispatcher();
        dispatcher.Attach(player);
        return (new City("Місто", player, dispatcher), player);
    }

    [Fact]
    public void BuildBuilding_FiftyFirstBuildingIsRejectedWithoutSpendingOrEvents()
    {
        var (city, player) = Create();
        for (var i = 0; i < 50; i++) city.BuildBuilding(FakeCatalog.Hut);
        var gold = player.Gold;
        Assert.False(city.CanBuildMore);
        var error = Assert.Throws<GameRuleException>(() => city.BuildBuilding(FakeCatalog.Hut));
        Assert.Contains("50", error.Message);
        Assert.Equal(50, city.GetBuildings().Count);
        Assert.Equal(gold, player.Gold);
        Assert.Equal(50, player.Statistics.BuildingsBuilt);
    }

    [Fact]
    public void RemoveBuilding_RefundsHalfAndRemovesPopulationAndIncome()
    {
        var (city, player) = Create();
        var building = city.BuildBuilding(FakeCatalog.Hut);
        var gold = player.Gold;
        var refund = city.RemoveBuilding(building);
        Assert.Equal(new ResourceAmounts(50), refund);
        Assert.Equal(gold + 50, player.Gold);
        Assert.Empty(city.GetBuildings());
        Assert.Equal(0, city.Population);
        city.Tick();
        Assert.Equal(gold + 50, player.Gold);
        Assert.Equal(1, player.Statistics.BuildingsBuilt);
        Assert.Equal(0, player.Statistics.TotalGoldEarned);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 90)]
    [InlineData(2, 130)]
    public void RemoveBuilding_RefundIncludesEveryUpgradeAndRoundsDown(int upgrades, int expectedRefund)
    {
        var (city, player) = Create();
        var building = city.BuildBuilding(FakeCatalog.Hut with { BuildCost = 101 });
        for (var i = 0; i < upgrades; i++) city.UpgradeBuilding(building);
        var gold = player.Gold;
        Assert.Equal(expectedRefund, building.DemolitionRefund);
        Assert.Equal(new ResourceAmounts(expectedRefund), city.RemoveBuilding(building));
        Assert.Equal(gold + expectedRefund, player.Gold);
    }

    [Fact]
    public void RestoredBuilding_RefundIncludesPreviouslyPurchasedUpgrades()
    {
        var (city, player) = Create();
        var building = BuildingFactory.Create(FakeCatalog.Hut with { BuildCost = 101 });
        building.Restore(Guid.NewGuid(), 3, BuildingStatus.MaxLevel);
        city.RestoreBuilding(building);
        var gold = player.Gold;
        Assert.Equal(new ResourceAmounts(130), city.RemoveBuilding(building));
        Assert.Equal(gold + 130, player.Gold);
    }
    [Fact]
    public void RemoveBuilding_CannotRefundTwiceOrRemoveAnotherCityBuilding()
    {
        var (city, player) = Create();
        var (otherCity, _) = Create();
        var foreignBuilding = otherCity.BuildBuilding(FakeCatalog.Hut);
        var gold = player.Gold;
        Assert.Throws<GameRuleException>(() => city.RemoveBuilding(foreignBuilding));
        Assert.Equal(gold, player.Gold);
        Assert.Single(otherCity.GetBuildings());
        var building = city.BuildBuilding(FakeCatalog.Hut);
        city.RemoveBuilding(building);
        gold = player.Gold;
        Assert.Throws<GameRuleException>(() => city.RemoveBuilding(building));
        Assert.Equal(gold, player.Gold);
    }

    [Fact]
    public void RemoveBuilding_AtCapacityAllowsAnotherBuild()
    {
        var (city, _) = Create();
        for (var i = 0; i < 50; i++) city.BuildBuilding(FakeCatalog.Hut);
        city.RemoveBuilding(city.GetBuildings()[0]);
        Assert.True(city.CanBuildMore);
        city.BuildBuilding(FakeCatalog.Hut);
        Assert.Equal(50, city.GetBuildings().Count);
        Assert.False(city.CanBuildMore);
    }

    [Fact]
    public void RestoredCity_StillEnforcesBuildLimit()
    {
        var (city, player) = Create();
        for (var i = 0; i < 50; i++)
        {
            var building = BuildingFactory.Create(FakeCatalog.Hut);
            building.Build();
            city.RestoreBuilding(building);
        }
        Assert.False(city.CanBuildMore);
        var gold = player.Gold;
        Assert.Throws<GameRuleException>(() => city.BuildBuilding(FakeCatalog.Hut));
        Assert.Equal(gold, player.Gold);
    }
}
