using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Microsoft.Data.Sqlite;

namespace Acut.Data.Mapping;

/// <summary>Перетворює рядки каталогу на доменні об'єкти.</summary>
internal static class CatalogMapper
{
    public static BuildingType ReadBuildingType(SqliteDataReader r) => new(
        r.GetInt("BuildingTypeId"),
        r.GetEnum<BuildingKind>("Kind"),
        r.GetStr("Name"),
        r.GetInt("BuildCost"),
        r.GetInt("UpgradeCost"),
        r.GetInt("MaxLevel"),
        r.GetInt("IncomePerTick"),
        r.GetNullableInt("Capacity") ?? 0,
        r.GetNullableStr("Resource"),
        r.GetNullableInt("ProductionRate") ?? 0,
        r.GetNullableInt("RequiredSkillId"));

    /// <summary>Підклас предмета визначається полем Kind.</summary>
    public static Item ReadItem(SqliteDataReader r)
    {
        var id = r.GetInt("ItemId");
        var name = r.GetStr("Name");
        var description = r.GetStr("Description");
        var price = r.GetInt("Price");
        var kind = r.GetStr("Kind");

        return kind switch
        {
            "Potion" => new Potion(id, name, description, price,
                Enum.Parse<PotionEffect>(r.GetStr("PotionEffect")), r.GetInt("EffectValue")),
            "Weapon" => new Weapon(id, name, description, price, r.GetInt("BonusAttack")),
            "Armor" => new Armor(id, name, description, price, r.GetInt("BonusDefense")),
            _ => throw new InvalidDataException($"Невідомий вид предмета «{kind}» (ItemId = {id}).")
        };
    }

    public static Skill ReadSkill(SqliteDataReader r) => new(
        r.GetInt("SkillId"), r.GetStr("Name"), r.GetStr("Description"), r.GetInt("Cost"));

    public static Quest ReadQuest(SqliteDataReader r) => new(
        r.GetInt("QuestId"), r.GetStr("Title"), r.GetStr("Description"),
        r.GetEnum<GameEventType>("EventType"), r.GetInt("Target"),
        r.GetInt("RewardGold"), r.GetInt("RewardExperience"));

    public static Achievement ReadAchievement(SqliteDataReader r) => new(
        r.GetInt("AchievementId"), r.GetStr("Title"), r.GetStr("Condition"),
        r.GetEnum<GameEventType>("EventType"), r.GetInt("Threshold"));

    public static EnemyDefinition ReadEnemy(SqliteDataReader r) => new(
        r.GetInt("EnemyId"), r.GetEnum<EnemyType>("EnemyType"), r.GetStr("Name"),
        r.GetInt("MaxHealth"), r.GetInt("AttackPower"), r.GetInt("Defense"),
        r.GetInt("RewardGold"), r.GetInt("RewardExperience"), r.GetEnum<StrategyType>("StrategyType"));

    /// <summary>Ціни в БД зберігаються в сотих, у домені - decimal.</summary>
    public static Stock ReadStock(SqliteDataReader r) => new(
        r.GetInt("StockId"), r.GetStr("Symbol"), r.GetStr("Name"),
        SaveMapper.FromCents(r.GetLong("BasePrice")), (decimal)r.GetDbl("Volatility"));
}
