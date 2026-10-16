namespace Acut.Core.Domain.Progress;

/// <summary>Типи подій. Імена збігаються зі значеннями EventType у QuestDefinition та AchievementDefinition.</summary>
public enum GameEventType
{
    BuildingBuilt,
    BuildingUpgraded,
    GoldEarned,
    BattleWon,
    BattleLost,
    PotionCrafted,
    ItemBought,
    SkillUnlocked
}
