using Acut.Core.Domain.Combat;

namespace Acut.Core.Domain.Characters;

public sealed class Enemy : Character
{
    public Enemy(
        string name, EnemyType type, int maxHealth, int attackPower, int defense,
        int rewardGold, int rewardExperience, IEnemyStrategy strategy, int? health = null, int definitionId = 0)
        : base(name, maxHealth, health ?? maxHealth, attackPower, defense)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        DefinitionId = definitionId;
        Type = type;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
        Strategy = strategy;
    }

    /// <summary>EnemyId у каталозі EnemyDefinition (потрібен для збереження активного бою).</summary>
    public int DefinitionId { get; }
    public EnemyType Type { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public IEnemyStrategy Strategy { get; }

    public BattleAction ChooseAction(Battle battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        return Strategy.ChooseAction(this, battle.Player);
    }
}
