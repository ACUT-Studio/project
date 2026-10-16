using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

/// <summary>Рядок каталогу ворогів (EnemyDefinition). Створює живий екземпляр Enemy для бою.</summary>
public sealed record EnemyDefinition(
    int Id, EnemyType Type, string Name, int MaxHealth, int AttackPower, int Defense,
    int RewardGold, int RewardExperience, StrategyType Strategy)
{
    public Enemy CreateEnemy(int? currentHealth = null) => new(
        Name, Type, MaxHealth, AttackPower, Defense, RewardGold, RewardExperience,
        EnemyStrategyFactory.Create(Strategy), currentHealth, Id);
}
