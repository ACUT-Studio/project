using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class ResourceEconomyTests
{
    private static (City City, Player Player) Create(ResourceAmounts? resources = null)
    {
        resources ??= new ResourceAmounts(10000, 10000, 10000);
        var player = new Player("Тест", AppearanceSettings.Default, gold: resources.Gold, wood: resources.Wood, stone: resources.Stone);
        var dispatcher = new GameEventDispatcher();
        dispatcher.Attach(player);
        return (new City("Місто", player, dispatcher, market: new StockMarket(new FakeCatalog().GetStocks()),
            shop: new Shop(new FakeCatalog(), dispatcher)), player);
    }

    private static BuildingType MixedType => FakeCatalog.Hut with
    {
        BuildCost = 101, BuildWoodCost = 51, BuildStoneCost = 31,
        UpgradeCost = 21, UpgradeWoodCost = 11, UpgradeStoneCost = 9
    };

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 100, 0)]
    public void SpendResources_MissingAnyResourceIsAtomic(int gold, int wood, int stone)
    {
        var (_, player) = Create(new(gold, wood, stone));
        var before = player.Resources;
        Assert.False(player.SpendResources(new(10, 10, 10)));
        Assert.Equal(before, player.Resources);
    }

    [Fact]
    public void BuildUpgradeAndSell_ChargeAndRefundAllResources()
    {
        var (city, player) = Create(new(1000, 1000, 1000));
        var building = city.BuildBuilding(MixedType);
        Assert.Equal(new ResourceAmounts(899, 949, 969), player.Resources);
        city.UpgradeBuilding(building);
        Assert.Equal(new ResourceAmounts(878, 938, 960), player.Resources);
        Assert.Equal(new ResourceAmounts(122, 62, 40), building.InvestedResources);
        Assert.Equal(new ResourceAmounts(61, 31, 20), city.RemoveBuilding(building));
        Assert.Equal(new ResourceAmounts(939, 969, 980), player.Resources);
    }

    [Fact]
    public void FailedBuildAndUpgrade_DoNotPartiallySpendOrChangeInvestment()
    {
        var (city, player) = Create(new(1000, 1000, 0));
        Assert.Throws<GameRuleException>(() => city.BuildBuilding(MixedType));
        Assert.Equal(new ResourceAmounts(1000, 1000, 0), player.Resources);
        var building = city.BuildBuilding(MixedType with { BuildStoneCost = 0 });
        var before = player.Resources;
        var investment = building.InvestedResources;
        Assert.False(city.TryUpgradeBuilding(building, out var reason));
        Assert.NotNull(reason);
        Assert.Equal(before, player.Resources);
        Assert.Equal(investment, building.InvestedResources);
        Assert.Equal(1, building.Level);
    }

    [Fact]
    public void Production_ProducesOnlyItsResourceAndSpecialBuildingsProduceNothing()
    {
        var (city, player) = Create();
        city.BuildBuilding(FakeCatalog.Hut);
        city.BuildBuilding(FakeCatalog.Hut with { Id = 10, Kind = BuildingKind.Production, Resource = "Wood", ProductionRate = 4, IncomePerTick = 99 });
        city.BuildBuilding(FakeCatalog.Hut with { Id = 11, Kind = BuildingKind.Production, Resource = "Stone", ProductionRate = 3, IncomePerTick = 99 });
        city.BuildBuilding(FakeCatalog.Workshop with { IncomePerTick = 99 });
        city.BuildBuilding(FakeCatalog.Workshop with { Id = 12, Kind = BuildingKind.Market, IncomePerTick = 99 });
        var before = player.Resources;
        Assert.Equal(new ResourceAmounts(2, 4, 3), city.CollectAllResources());
        Assert.Equal(before.Add(new(2, 4, 3)), player.Resources);
        Assert.Equal(2, player.Statistics.TotalGoldEarned);
    }

    [Fact]
    public void FreeBuilding_RefundsOnlyPurchasedUpgrades()
    {
        var (city, _) = Create();
        var sawmill = BuildingFactory.Create(MixedType with { Kind = BuildingKind.Production, Resource = "Wood", ProductionRate = 4 });
        sawmill.Build(isFree: true);
        city.RestoreBuilding(sawmill);
        Assert.True(sawmill.InvestedResources.IsEmpty);
        Assert.True(sawmill.DemolitionResources.IsEmpty);
        city.UpgradeBuilding(sawmill);
        Assert.Equal(new ResourceAmounts(21, 11, 9), sawmill.InvestedResources);
        Assert.Equal(new ResourceAmounts(10, 5, 4), city.RemoveBuilding(sawmill));
    }

    [Theory]
    [InlineData(BuildingKind.Workshop)]
    [InlineData(BuildingKind.Market)]
    public void SpecialBuildings_AreUniqueByKindAndCanBeRebuiltAfterSale(BuildingKind kind)
    {
        var (city, player) = Create();
        var type = FakeCatalog.Workshop with { Kind = kind };
        var first = city.BuildBuilding(type);
        var before = player.Resources;
        Assert.Throws<GameRuleException>(() => city.BuildBuilding(type with { Id = 99 }));
        Assert.Equal(before, player.Resources);
        city.RemoveBuilding(first);
        city.BuildBuilding(type);
        Assert.Single(city.GetBuildings());
    }

    [Fact]
    public void Mechanics_RequireOwnedBuildingAndAreRevokedAfterSale()
    {
        var (city, player) = Create();
        player.Inventory.AddItem(FakeCatalog.SmallHealPotion, 4);
        Assert.Throws<GameRuleException>(() => city.Craft(FakeCatalog.LargeHealRecipe));
        Assert.False(city.Market.Buy(city.Market.Stocks[0], 1, player));
        Assert.False(city.Shop!.BuyItem(FakeCatalog.SmallHealPotion, player));

        var workshop = (WorkshopBuilding)city.BuildBuilding(FakeCatalog.Workshop);
        var market = city.BuildBuilding(FakeCatalog.Workshop with { Id = 99, Kind = BuildingKind.Market });
        Assert.True(city.HasWorkshop);
        Assert.True(city.HasMarket);
        Assert.NotNull(city.Craft(FakeCatalog.LargeHealRecipe));
        Assert.True(city.Market.Buy(city.Market.Stocks[0], 1, player));
        Assert.True(city.Shop.BuyItem(FakeCatalog.SmallHealPotion, player));
        city.RemoveBuilding(workshop);
        city.RemoveBuilding(market);
        var before = player.Resources;
        Assert.Null(workshop.Craft(FakeCatalog.LargeHealRecipe, player.Inventory));
        Assert.False(city.Market.Sell(city.Market.Stocks[0], 1, player));
        Assert.False(city.Shop.BuyItem(FakeCatalog.SmallHealPotion, player));
        Assert.Equal(before, player.Resources);
    }
}
