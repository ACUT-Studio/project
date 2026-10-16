using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;

namespace Acut.Core.Abstractions;

/// <summary>
/// Читання довідкових даних (каталогу контенту). Кожен виклик повертає нові екземпляри,
/// тому змінні об'єкти (рівень предмета, стан навички) не розділяються між сесіями.
/// </summary>
public interface ICatalogRepository
{
    IReadOnlyList<BuildingType> GetBuildingTypes();
    IReadOnlyList<Item> GetItems();
    IReadOnlyList<Recipe> GetRecipes();
    IReadOnlyList<Skill> GetSkills();
    IReadOnlyList<Quest> GetQuests();
    IReadOnlyList<Achievement> GetAchievements();
    IReadOnlyList<EnemyDefinition> GetEnemies();
    IReadOnlyList<Stock> GetStocks();
}
