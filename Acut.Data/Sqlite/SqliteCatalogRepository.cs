using Acut.Core.Abstractions;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Data.Mapping;

namespace Acut.Data.Sqlite;

/// <summary>Читання довідкових даних каталогу. Щоразу повертає нові екземпляри.</summary>
public sealed class SqliteCatalogRepository : ICatalogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteCatalogRepository(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IReadOnlyList<BuildingType> GetBuildingTypes()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM BuildingType ORDER BY BuildingTypeId", CatalogMapper.ReadBuildingType);
    }

    public IReadOnlyList<Item> GetItems()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM ItemDefinition ORDER BY ItemId", CatalogMapper.ReadItem);
    }

    public IReadOnlyList<Recipe> GetRecipes()
    {
        var items = GetItems().ToDictionary(i => i.Id);
        using var connection = _factory.CreateConnection();

        var ingredients = new Dictionary<int, Dictionary<Item, int>>();
        SqlHelper.Read(connection, "SELECT RecipeId, ItemId, Quantity FROM RecipeIngredient", r =>
        {
            var recipeId = r.GetInt("RecipeId");
            if (!ingredients.TryGetValue(recipeId, out var map))
                ingredients[recipeId] = map = new Dictionary<Item, int>();
            map[items[r.GetInt("ItemId")]] = r.GetInt("Quantity");
        });

        return SqlHelper.Query(connection, "SELECT RecipeId, Name, ResultItemId FROM Recipe ORDER BY RecipeId", r =>
        {
            var id = r.GetInt("RecipeId");
            var result = items[r.GetInt("ResultItemId")] as Potion
                ?? throw new InvalidDataException($"Результат рецепта {id} має бути зіллям.");
            return new Recipe(id, r.GetStr("Name"), result,
                ingredients.TryGetValue(id, out var map) ? map : new Dictionary<Item, int>());
        });
    }

    public IReadOnlyList<Skill> GetSkills()
    {
        var buildingTypes = GetBuildingTypes();
        using var connection = _factory.CreateConnection();

        var skills = SqlHelper.Query(connection, "SELECT * FROM Skill ORDER BY SkillId", CatalogMapper.ReadSkill);
        var byId = skills.ToDictionary(s => s.Id);

        SqlHelper.Read(connection, "SELECT SkillId, RequiredSkillId FROM SkillPrerequisite", r =>
            byId[r.GetInt("SkillId")].Prerequisites.Add(byId[r.GetInt("RequiredSkillId")]));

        foreach (var skill in skills)
            skill.UnlocksBuildings = buildingTypes.Where(b => b.RequiredSkillId == skill.Id).ToList();

        return skills;
    }

    public IReadOnlyList<Quest> GetQuests()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM QuestDefinition ORDER BY QuestId", CatalogMapper.ReadQuest);
    }

    public IReadOnlyList<Achievement> GetAchievements()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM AchievementDefinition ORDER BY AchievementId", CatalogMapper.ReadAchievement);
    }

    public IReadOnlyList<EnemyDefinition> GetEnemies()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM EnemyDefinition ORDER BY EnemyId", CatalogMapper.ReadEnemy);
    }

    public IReadOnlyList<Stock> GetStocks()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection, "SELECT * FROM Stock ORDER BY StockId", CatalogMapper.ReadStock);
    }
}
