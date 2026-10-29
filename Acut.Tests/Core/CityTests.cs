using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class CityTests
{
    private static (City City, Player Player, GameEventDispatcher Dispatcher) Create(int gold)
    {
        var player = new Player("Тест", AppearanceSettings.Default, gold: gold);
        var dispatcher = new GameEventDispatcher();
        dispatcher.Attach(player);
        return (new City("Місто", player, dispatcher), player, dispatcher);
    }


    [Fact]
    public void BuildBuilding_NotEnoughGold_ThrowsAndKeepsState()
    {
       
        var (city, player, _) = Create(gold: 50);

        Assert.Throws<GameRuleException>(() => city.BuildBuilding(FakeCatalog.Hut));

        Assert.Equal(50, player.Gold);
        Assert.Empty(city.GetBuildings());
    }

    [Fact]
    public void BuildBuilding_EnoughGold_SpendsGoldAddsActiveBuildingAndCountsStatistics()
    {
      
        var (city, player, _) = Create(gold: 250);

        var building = city.BuildBuilding(FakeCatalog.Hut);

        Assert.Equal(150, player.Gold);
        Assert.Equal(BuildingStatus.Active, building.Status);
        Assert.Single(city.GetBuildings());
        Assert.Equal(1, player.Statistics.BuildingsBuilt);
    }

    [Fact]
    public void BuildBuilding_RequiredSkillNotUnlocked_ThrowsAndKeepsGold()
    {
       
        var (city, player, _) = Create(gold: 1000);
        player.SkillTree.Initialize(new FakeCatalog().GetSkills());

        Assert.Throws<GameRuleException>(() => city.BuildBuilding(FakeCatalog.Mine));

        Assert.Equal(1000, player.Gold);
        Assert.Empty(city.GetBuildings());
    }

    [Fact]
    public void Tick_ActiveBuilding_PaysIncomeAndCountsGoldEarned()
    {
       
        var (city, player, _) = Create(gold: 100);
        city.BuildBuilding(FakeCatalog.Hut);
        var goldAfterBuild = player.Gold;

        city.Tick();
       
        Assert.Equal(goldAfterBuild + FakeCatalog.Hut.IncomePerTick, player.Gold);
        Assert.Equal(FakeCatalog.Hut.IncomePerTick, player.Statistics.TotalGoldEarned);
    }

    [Fact]
    public void Population_SumsActiveResidentialCapacity()
    {
       
        var (city, _, _) = Create(gold: 500);

        city.BuildBuilding(FakeCatalog.Hut);
        city.BuildBuilding(FakeCatalog.Hut);

        Assert.Equal(8, city.Population);
    }

   
    [Fact]
    public void BuildBuilding_MultipleBuildings_IncrementsBuiltStatisticsCorrectly()
    {
       
        var (city, player, _) = Create(gold: 1000);

        city.BuildBuilding(FakeCatalog.Hut);
        city.BuildBuilding(FakeCatalog.Hut);

        Assert.Equal(2, player.Statistics.BuildingsBuilt);
        Assert.Equal(2, city.GetBuildings().Count());
    }

    [Fact]
    public void Tick_MultipleTicks_AccumulatesIncomeCorrectly()
    {
       
        var (city, player, _) = Create(gold: 100);
        city.BuildBuilding(FakeCatalog.Hut);
        var initialGold = player.Gold;
        var incomePerTick = FakeCatalog.Hut.IncomePerTick;

        city.Tick();
        city.Tick();
        city.Tick();

        Assert.Equal(initialGold + (incomePerTick * 3), player.Gold);
        Assert.Equal(incomePerTick * 3, player.Statistics.TotalGoldEarned);
    }

    [Fact]
    public void Tick_NoBuildings_DoesNotChangeGoldOrStatistics()
    {
       
        var (city, player, _) = Create(gold: 100);

        city.Tick();

        Assert.Equal(100, player.Gold);
        Assert.Equal(0, player.Statistics.TotalGoldEarned);
    }

    [Fact]
    public void Population_NoBuildings_ReturnsZero()
    {
        
        var (city, _, _) = Create(gold: 100);

        Assert.Equal(0, city.Population);
    }

    
   
}