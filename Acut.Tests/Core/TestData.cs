using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;

namespace Acut.Tests.Core;

/// <summary>Мінімальний каталог у пам'яті для тестів ядра (без БД).</summary>
internal sealed class FakeCatalog : ICatalogRepository
{
    public static readonly BuildingType Hut = new(1, BuildingKind.Residential, "Хатина", 100, 80, 3, 2, 4, null, 0, null);
    public static readonly BuildingType Mine = new(2, BuildingKind.Production, "Шахта", 400, 300, 3, 10, 0, "Ore", 1, 1);

    public IReadOnlyList<BuildingType> GetBuildingTypes() => new[] { Hut, Mine };
    public IReadOnlyList<Item> GetItems() => Array.Empty<Item>();
    public IReadOnlyList<Recipe> GetRecipes() => Array.Empty<Recipe>();

    public IReadOnlyList<Skill> GetSkills() => new[] { new Skill(1, "Основи будівництва", "", 1) };

    public IReadOnlyList<Quest> GetQuests() =>
        new[] { new Quest(1, "Перша будівля", "", GameEventType.BuildingBuilt, 1, 50, 20) };

    public IReadOnlyList<Achievement> GetAchievements() =>
        new[] { new Achievement(1, "Забудовник", "", GameEventType.BuildingBuilt, 2) };

    public IReadOnlyList<EnemyDefinition> GetEnemies() => Array.Empty<EnemyDefinition>();
    public IReadOnlyList<Stock> GetStocks() => Array.Empty<Stock>();
}