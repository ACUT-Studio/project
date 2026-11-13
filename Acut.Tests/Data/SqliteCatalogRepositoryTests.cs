using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Xunit;

namespace Acut.Tests.Data;

public sealed class SqliteCatalogRepositoryTests
{
    [Fact]
    public void CatalogRepository_ReadsAllSeedEntitiesWithRelationships()
    {
        using var db = new TestDatabase();
        var catalog = db.Catalog;

        var economy = catalog.GetNewGameEconomy();
        Assert.Equal(new ResourceAmounts(0, 150, 0), economy.Resources);
        Assert.Equal(3, economy.StartingBuildingTypeId);

        var buildings = catalog.GetBuildingTypes();
        var items = catalog.GetItems();
        var recipes = catalog.GetRecipes();
        var skills = catalog.GetSkills();
        var quests = catalog.GetQuests();
        var achievements = catalog.GetAchievements();
        var enemies = catalog.GetEnemies();
        var stocks = catalog.GetStocks();

        Assert.Equal(6, buildings.Count);
        Assert.Equal(7, items.Count);
        Assert.Contains(items, i => i is Potion);
        Assert.Contains(items, i => i is Weapon);
        Assert.Contains(items, i => i is Armor);
        Assert.Equal(2, recipes.Count);
        Assert.All(recipes, r => Assert.NotEmpty(r.Ingredients));
        Assert.Equal(3, skills.Count);
        Assert.Contains(skills, s => s.Prerequisites.Count > 0);
        Assert.Equal(5, quests.Count);
        Assert.Equal(5, achievements.Count);
        Assert.Equal(4, enemies.Count);
        Assert.Equal(3, stocks.Count);
    }

    [Fact]
    public void CatalogRepository_ReturnsFreshInstancesOnEveryCall()
    {
        using var db = new TestDatabase();

        var firstItems = db.Catalog.GetItems();
        var secondItems = db.Catalog.GetItems();
        firstItems[0].Upgrade();

        Assert.Equal(2, firstItems[0].Level);
        Assert.Equal(1, secondItems[0].Level);

        var firstStocks = db.Catalog.GetStocks();
        var secondStocks = db.Catalog.GetStocks();
        firstStocks[0].RestorePrice(999m);

        Assert.NotEqual(firstStocks[0].Price, secondStocks[0].Price);
    }
}