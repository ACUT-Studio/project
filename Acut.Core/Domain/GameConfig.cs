namespace Acut.Core.Domain;

/// <summary>Базові числові налаштування гри. Баланс каталогу контенту лежить у seed.sql.</summary>
public static class GameConfig
{
    public const int StartingGold = 500;
    public const int StartingSkillPoints = 1;

    public const int PlayerBaseHealth = 100;
    public const int PlayerBaseAttack = 10;
    public const int PlayerBaseDefense = 2;

    public const int ExperiencePerLevel = 100;
    public const int HealthPerLevel = 10;
    public const int AttackPerLevel = 2;
    public const int DefensePerLevel = 1;

    public const int InventoryCapacity = 30;
    public const int MaxPlayerNameLength = 24;
    public const int MaxItemLevel = 5;
}
