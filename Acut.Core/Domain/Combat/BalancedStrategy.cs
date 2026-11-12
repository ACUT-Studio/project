using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

public sealed class BalancedStrategy : IEnemyStrategy
{
    public BattleAction ChooseAction(Enemy self, Player target)
    {
        return self.Health * 3 <= self.MaxHealth ? BattleAction.Defend : BattleAction.Attack;
    }
}
