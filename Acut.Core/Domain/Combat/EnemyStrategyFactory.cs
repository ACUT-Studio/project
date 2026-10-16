namespace Acut.Core.Domain.Combat;

/// <summary>Створює стратегію за значенням StrategyType з каталогу ворогів.</summary>
public static class EnemyStrategyFactory
{
    public static IEnemyStrategy Create(StrategyType type) => type switch
    {
        StrategyType.Aggressive => new AggressiveStrategy(),
        StrategyType.Defensive => new DefensiveStrategy(),
        StrategyType.Balanced => new BalancedStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Невідома стратегія ворога.")
    };
}
