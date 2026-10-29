using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;

namespace Acut.Core.Domain.Characters;

public sealed class Player : Character
{
    public Player(
        string name, AppearanceSettings appearance,
        int level = 1, int experience = 0, int gold = 0,
        int health = GameConfig.PlayerBaseHealth, int maxHealth = GameConfig.PlayerBaseHealth,
        int attackPower = GameConfig.PlayerBaseAttack, int defense = GameConfig.PlayerBaseDefense)
        : base(name, maxHealth, health, attackPower, defense)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        ArgumentOutOfRangeException.ThrowIfNegative(gold);

        Appearance = appearance;
        Level = level;
        Experience = experience;
        Gold = gold;

        Inventory = new Inventory();
        SkillTree = new SkillTree();
        QuestLog = new QuestLog { Player = this };
        Statistics = new PlayerStatistics();
        Achievements = new AchievementTracker(Statistics);
    }

    public int Level { get; private set; }
    public int Experience { get; private set; }
    public int Gold { get; private set; }
    public AppearanceSettings Appearance { get; }

    public Inventory Inventory { get; }
    public SkillTree SkillTree { get; }
    public QuestLog QuestLog { get; }
    public AchievementTracker Achievements { get; }
    public PlayerStatistics Statistics { get; }

    /// <summary>Пакети акцій гравця: StockId → кількість.</summary>
    public Dictionary<int, int> StockHoldings { get; } = new();

    public int SkillPoints => SkillTree.SkillPoints;
    public int ExperienceForNextLevel => Level * GameConfig.ExperiencePerLevel;

    public void GainExperience(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        Experience += xp;
        while (Experience >= ExperienceForNextLevel)
        {
            Experience -= ExperienceForNextLevel;
            LevelUp();
        }
    }

    public void LevelUp()
    {
        Level++;
        MaxHealth += GameConfig.HealthPerLevel;
        Health = MaxHealth;
        AttackPower += GameConfig.AttackPerLevel;
        Defense += GameConfig.DefensePerLevel;
        SkillTree.SkillPoints++;
    }

    public void AddGold(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Gold += amount;
    }

    /// <summary>Списує валюту. Повертає false, якщо її недостатньо (баланс не змінюється).</summary>
    public bool SpendGold(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }
}
