using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

public sealed class BalancedStrategy : IEnemyStrategy
{
    public BattleAction ChooseAction(Enemy self, Player target)
    {
        // TODO(Етап 6): логіка вибору дії для стратегії Balanced.
        throw new NotImplementedException("Етап 6: BalancedStrategy.ChooseAction");
    }
}
