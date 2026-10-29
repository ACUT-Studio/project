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
    public static readonly BuildingType Workshop = new(3, BuildingKind.Workshop, "Майстерня", 600, 450, 3, 0, 0, null, 0, null);

    public static readonly Potion SmallHealPotion = new(1, "Мале зілля лікування", "Відновлює 25 HP", 30, PotionEffect.Heal, 25);
    public static readonly Potion LargeHealPotion = new(2, "Велике зілля лікування", "Відновлює 60 HP", 80, PotionEffect.Heal, 60);
    public static readonly Weapon IronSword = new(4, "Залізний меч", "Додає 5 до атаки", 150, 5);
    public static readonly Armor LeatherArmor = new(6, "Шкіряна броня", "Додає 3 до захисту", 120, 3);

    public static readonly Recipe LargeHealRecipe = new(
        1,
        "Велике зілля лікування",
        LargeHealPotion,
        new Dictionary<Item, int> { [SmallHealPotion] = 2 });

    public static readonly Stock AgroStock = new(1, "AGRO", "Агро-Холдинг", 100m, 0.05m);
    public static readonly Stock MineStock = new(2, "MINE", "ГірМаш", 250m, 0.10m);

    public IReadOnlyList<BuildingType> GetBuildingTypes() => new[] { Hut, Mine, Workshop };
    public IReadOnlyList<Item> GetItems() => new Item[] { SmallHealPotion, LargeHealPotion, IronSword, LeatherArmor };
    public IReadOnlyList<Recipe> GetRecipes() => new[] { LargeHealRecipe };

    public IReadOnlyList<Skill> GetSkills() => new[] { new Skill(1, "Основи будівництва", "", 1) };

    public IReadOnlyList<Quest> GetQuests() =>
        new[] { new Quest(1, "Перша будівля", "", GameEventType.BuildingBuilt, 1, 50, 20) };

    public IReadOnlyList<Achievement> GetAchievements() =>
        new[] { new Achievement(1, "Забудовник", "", GameEventType.BuildingBuilt, 2) };

    public IReadOnlyList<EnemyDefinition> GetEnemies() => Array.Empty<EnemyDefinition>();
    public IReadOnlyList<Stock> GetStocks() => new[] { AgroStock, MineStock };
}
