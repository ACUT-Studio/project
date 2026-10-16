namespace Acut.Core.Domain.Combat;

/// <summary>Життєвий цикл бою (діаграма станів 2.2).</summary>
public enum BattleState
{
    Initialized,
    PlayerTurn,
    EnemyTurn,
    Finished
}
