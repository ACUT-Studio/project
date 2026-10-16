using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Combat;

public interface IEnemyStrategy
{
    BattleAction ChooseAction(Enemy self, Player target);
}
