using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

public sealed class DefensiveStrategy : IEnemyStrategy
{
    public BattleAction ChooseAction(Enemy self, Player target)
    {
        if (self.Health * 4 <= self.MaxHealth) return BattleAction.Defend;
        return target.Health * 2 <= target.MaxHealth ? BattleAction.Defend : BattleAction.Attack;
    }
}
