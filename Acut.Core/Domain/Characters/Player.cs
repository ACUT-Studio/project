using Acut.Core.Domain.Items;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Progress;

namespace Acut.Core.Domain.Characters;

public sealed class Player : Character
{
    private int? _equippedWeaponId;
    private int? _equippedArmorId;

    public Player(
        string name, AppearanceSettings appearance,
        int level = 1, int experience = 0, int gold = 0,
        int health = GameConfig.PlayerBaseHealth, int maxHealth = GameConfig.PlayerBaseHealth,
        int attackPower = GameConfig.PlayerBaseAttack, int defense = GameConfig.PlayerBaseDefense,
        int wood = 0, int stone = 0)
        : base(name, maxHealth, health, attackPower, defense)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        ArgumentOutOfRangeException.ThrowIfNegative(gold);

        ArgumentOutOfRangeException.ThrowIfNegative(wood);
        ArgumentOutOfRangeException.ThrowIfNegative(stone);
        Wood = wood;
        Stone = stone;
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
    public int Wood { get; private set; }
    public int Stone { get; private set; }
    public ResourceAmounts Resources => new(Gold, Wood, Stone);

    public bool CanAfford(ResourceAmounts cost) =>
        Gold >= cost.Gold && Wood >= cost.Wood && Stone >= cost.Stone;

    /// <summary>Списує всі складові атомарно: за нестачі хоч одного ресурсу нічого не змінює.</summary>
    public bool SpendResources(ResourceAmounts cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        if (!CanAfford(cost)) return false;
        Gold -= cost.Gold;
        Wood -= cost.Wood;
        Stone -= cost.Stone;
        return true;
    }

    public void AddResources(ResourceAmounts amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        var total = Resources.Add(amount);
        Gold = total.Gold;
        Wood = total.Wood;
        Stone = total.Stone;
    }
    public AppearanceSettings Appearance { get; }

    public Inventory Inventory { get; }
    public Weapon? EquippedWeapon => Inventory.GetItems().FirstOrDefault(s => s.Item.Id == _equippedWeaponId)?.Item as Weapon;
    public Armor? EquippedArmor => Inventory.GetItems().FirstOrDefault(s => s.Item.Id == _equippedArmorId)?.Item as Armor;
    public int TotalAttackPower => AttackPower + (EquippedWeapon?.BonusAttack ?? 0);
    public int TotalDefense => Defense + (EquippedArmor?.BonusDefense ?? 0);

    /// <summary>Вдягає власний предмет інвентаря, замінюючи спорядження того самого типу.</summary>
    public bool EquipItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var owned = Inventory.GetItems().FirstOrDefault(s => s.Item.Id == item.Id)?.Item;
        switch (owned)
        {
            case Weapon weapon: _equippedWeaponId = weapon.Id; return true;
            case Armor armor: _equippedArmorId = armor.Id; return true;
            default: return false;
        }
    }

    public void UnequipWeapon() => _equippedWeaponId = null;
    public void UnequipArmor() => _equippedArmorId = null;

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
