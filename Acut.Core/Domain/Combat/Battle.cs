using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Services;

namespace Acut.Core.Domain.Combat;

/// <summary>Покроковий бій: кожна успішна дія передає хід іншому учаснику.</summary>
public sealed class Battle
{
    private BattleResult _outcome;

    public Battle(Player player, Enemy enemy, GameEventDispatcher? dispatcher = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        TurnNumber = 1;
        State = BattleState.Initialized;
    }

    public Player Player { get; }
    public Enemy Enemy { get; }
    public int TurnNumber { get; private set; }
    public BattleState State { get; private set; }
    public bool IsPlayerTurn { get; private set; } = true;
    public bool PlayerDefending { get; private set; }
    public bool EnemyDefending { get; private set; }

    public void Start()
    {
        if (State != BattleState.Initialized)
            throw new InvalidOperationException("Бій уже розпочато.");
        if (!Player.IsAlive || !Enemy.IsAlive)
            throw new GameRuleException("Учасники мають бути живими на початку бою.");
        State = BattleState.PlayerTurn;
        IsPlayerTurn = true;
    }

    /// <summary>Відновлює позицію в бою після завантаження збереження.</summary>
    public void Restore(int turnNumber, bool isPlayerTurn, bool playerDefending = false, bool enemyDefending = false, bool finished = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, 1);
        TurnNumber = turnNumber;
        IsPlayerTurn = isPlayerTurn;
        PlayerDefending = playerDefending;
        EnemyDefending = enemyDefending;
        State = finished || CheckOutcome() != BattleResult.InProgress ? BattleState.Finished
            : isPlayerTurn ? BattleState.PlayerTurn : BattleState.EnemyTurn;
        if (State == BattleState.Finished)
            _outcome = !Enemy.IsAlive ? BattleResult.Victory : BattleResult.Defeat;
    }

    public bool CanExecutePlayerAction(BattleAction action, Item? item = null) =>
        State == BattleState.PlayerTurn && CheckOutcome() == BattleResult.InProgress && (action switch
        {
            BattleAction.Attack or BattleAction.Defend => true,
            BattleAction.Heal or BattleAction.UseItem => Player.Health < Player.MaxHealth &&
                Player.Inventory.GetItems().Any(s => s.Item.Id == item?.Id && s.Item is Potion { Effect: PotionEffect.Heal, EffectValue: > 0 }),
            _ => false
        });

    public ActionResult ExecutePlayerAction(BattleAction action, Item? item = null)
    {
        if (!CanExecutePlayerAction(action, item))
            throw new GameRuleException("Ця дія зараз недоступна.");
        PlayerDefending = false;
        var result = Resolve(action, Player, Enemy, EnemyDefending, item);
        if (action == BattleAction.Defend) PlayerDefending = true;
        NextTurn();
        return result;
    }

    public ActionResult ExecuteEnemyAction()
    {
        if (State != BattleState.EnemyTurn || CheckOutcome() != BattleResult.InProgress)
            throw new GameRuleException("Зараз не хід ворога.");
        var action = Enemy.ChooseAction(this);
        if (action is not (BattleAction.Attack or BattleAction.Defend))
            throw new GameRuleException("Стратегія обрала недоступну дію.");
        EnemyDefending = false;
        var result = Resolve(action, Enemy, Player, PlayerDefending);
        if (action == BattleAction.Defend) EnemyDefending = true;
        NextTurn();
        return result;
    }

    public void NextTurn()
    {
        if (State is BattleState.Initialized or BattleState.Finished)
            throw new GameRuleException("Не можна перейти до наступного ходу.");
        if (CheckOutcome() != BattleResult.InProgress)
        {
            _outcome = CheckOutcome();
            State = BattleState.Finished;
            return;
        }
        IsPlayerTurn = !IsPlayerTurn;
        if (IsPlayerTurn) TurnNumber++;
        State = IsPlayerTurn ? BattleState.PlayerTurn : BattleState.EnemyTurn;
    }

    private ActionResult Resolve(BattleAction action, Character actor, Character target, bool defending, Item? item = null)
    {
        var damage = 0;
        var before = actor.Health;
        string message;
        switch (action)
        {
            case BattleAction.Attack:
                var attack = actor is Player attacker ? attacker.TotalAttackPower : actor.AttackPower;
                var defense = target is Player defender ? defender.TotalDefense : target.Defense;
                damage = Math.Min(target.Health, Math.Max(1, attack - defense * (defending ? 2 : 1)));
                target.TakeDamage(damage);
                message = $"{actor.Name}: атака — {damage} шкоди.";
                break;
            case BattleAction.Defend:
                message = $"{actor.Name}: захист подвоєно до наступної дії.";
                break;
            case BattleAction.Heal:
            case BattleAction.UseItem:
                var potion = (Potion)Player.Inventory.GetItems().Single(s => s.Item.Id == item!.Id).Item;
                Player.Inventory.RemoveItem(potion);
                potion.Use(actor);
                message = $"{actor.Name}: {potion.Name}, відновлено {actor.Health - before} здоров'я.";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
        return new ActionResult(action, damage, actor.Health - before, message);
    }

    public BattleResult CheckOutcome()
    {
        if (_outcome != BattleResult.InProgress) return _outcome;
        if (!Enemy.IsAlive) return BattleResult.Victory;
        if (!Player.IsAlive) return BattleResult.Defeat;
        return BattleResult.InProgress;
    }
}
