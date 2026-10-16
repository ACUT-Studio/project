using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Services;

namespace Acut.Core.Domain.Combat;

/// <summary>Покрокова логіка бою. Етап 5: каркас і життєвий цикл; дії та підрахунок шкоди — етап 6.</summary>
public sealed class Battle
{
    private readonly GameEventDispatcher? _dispatcher;

    public Battle(Player player, Enemy enemy, GameEventDispatcher? dispatcher = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        _dispatcher = dispatcher;
        TurnNumber = 1;
        State = BattleState.Initialized;
    }

    public Player Player { get; }
    public Enemy Enemy { get; }
    public int TurnNumber { get; private set; }
    public BattleState State { get; private set; }
    public bool IsPlayerTurn { get; private set; } = true;

    public void Start()
    {
        if (State != BattleState.Initialized)
            throw new InvalidOperationException("Бій уже розпочато.");
        State = BattleState.PlayerTurn;
        IsPlayerTurn = true;
    }

    /// <summary>Відновлює позицію в бою після завантаження збереження.</summary>
    public void Restore(int turnNumber, bool isPlayerTurn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, 1);
        TurnNumber = turnNumber;
        IsPlayerTurn = isPlayerTurn;
        State = isPlayerTurn ? BattleState.PlayerTurn : BattleState.EnemyTurn;
    }

    public ActionResult ExecutePlayerAction(BattleAction action, Item? item = null)
    {
        // TODO(Етап 6): Attack, Defend, Heal, UseItem + перевірка стану.
        throw new NotImplementedException("Етап 6: Battle.ExecutePlayerAction");
    }

    public ActionResult ExecuteEnemyAction()
    {
        // TODO(Етап 6): дія ворога за стратегією.
        throw new NotImplementedException("Етап 6: Battle.ExecuteEnemyAction");
    }

    public void NextTurn()
    {
        // TODO(Етап 6): перехід ходу, TurnNumber++.
        throw new NotImplementedException("Етап 6: Battle.NextTurn");
    }

    public BattleResult CheckOutcome()
    {
        if (!Enemy.IsAlive) return BattleResult.Victory;
        if (!Player.IsAlive) return BattleResult.Defeat;
        return BattleResult.InProgress;
    }
}
