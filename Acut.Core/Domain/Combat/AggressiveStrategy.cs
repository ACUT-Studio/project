using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

public sealed class AggressiveStrategy : IEnemyStrategy
{
    public BattleAction ChooseAction(Enemy self, Player target)
    {
        return BattleAction.Attack;
    }
}
