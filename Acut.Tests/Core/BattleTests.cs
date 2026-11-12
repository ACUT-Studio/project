using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class BattleTests
{
    [Fact]
    public void EquippedItems_ContributeToBattleAttackAndDefense()
    {
        var player = Player();
        var weapon = FakeCatalog.IronSword.Clone();
        var armor = FakeCatalog.LeatherArmor.Clone();
        player.Inventory.AddItem(weapon);
        player.Inventory.AddItem(armor);
        player.EquipItem(weapon);
        player.EquipItem(armor);
        var battle = new Battle(player, Enemy());
        battle.Start();
        Assert.Equal(12, battle.ExecutePlayerAction(BattleAction.Attack).Damage);
        Assert.Equal(5, battle.ExecuteEnemyAction().Damage);
        battle.ExecutePlayerAction(BattleAction.Defend);
        Assert.Equal(1, battle.ExecuteEnemyAction().Damage);
    }

    private static Player Player(int health = 100, int attack = 10) =>
        new("Герой", AppearanceSettings.Default, health: health, attackPower: attack);

    private static Enemy Enemy(int health = 50, int attack = 10, int defense = 3, IEnemyStrategy? strategy = null) =>
        new("Ворог", EnemyType.Goblin, health, attack, defense, 30, 20, strategy ?? new AggressiveStrategy());

    [Fact]
    public void Attack_SubtractsDefenseAndAlternatesTurns()
    {
        var battle = new Battle(Player(), Enemy());
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.Attack));
        battle.Start();
        Assert.Equal(7, battle.ExecutePlayerAction(BattleAction.Attack).Damage);
        Assert.Equal(BattleState.EnemyTurn, battle.State);
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.Attack));
        Assert.Equal(8, battle.ExecuteEnemyAction().Damage);
        Assert.Equal(BattleState.PlayerTurn, battle.State);
        Assert.Equal(2, battle.TurnNumber);
        Assert.Throws<GameRuleException>(() => battle.ExecuteEnemyAction());
    }

    [Fact]
    public void Defend_DoublesDefenseOnlyUntilNextOwnAction()
    {
        var battle = new Battle(Player(), Enemy());
        battle.Start();
        battle.ExecutePlayerAction(BattleAction.Defend);
        Assert.Equal(6, battle.ExecuteEnemyAction().Damage);
        Assert.True(battle.PlayerDefending);
        battle.ExecutePlayerAction(BattleAction.Attack);
        Assert.False(battle.PlayerDefending);
        Assert.Equal(8, battle.ExecuteEnemyAction().Damage);
    }

    [Fact]
    public void EnemyDefense_AppliesToNextPlayerAttack()
    {
        var player = Player(health: 40);
        var battle = new Battle(player, Enemy(strategy: new DefensiveStrategy()));
        battle.Start();
        battle.ExecutePlayerAction(BattleAction.Defend);
        Assert.Equal(BattleAction.Defend, battle.ExecuteEnemyAction().Action);
        Assert.Equal(4, battle.ExecutePlayerAction(BattleAction.Attack).Damage);
    }

    [Fact]
    public void Heal_RequiresOwnedPotionAndRejectsFullHealthWithoutConsumingPotionOrTurn()
    {
        var battle = new Battle(Player(95), Enemy());
        battle.Start();
        Assert.False(battle.CanExecutePlayerAction(BattleAction.Heal));
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.Heal));
        Assert.False(battle.CanExecutePlayerAction(BattleAction.Heal, FakeCatalog.SmallHealPotion));
        battle.Player.Inventory.AddItem(FakeCatalog.SmallHealPotion.Clone(), 2);
        Assert.Equal(5, battle.ExecutePlayerAction(BattleAction.Heal, FakeCatalog.SmallHealPotion).HealedAmount);
        Assert.Equal(100, battle.Player.Health);
        Assert.Equal(1, battle.Player.Inventory.GetQuantity(FakeCatalog.SmallHealPotion));
        battle.ExecuteEnemyAction();
        battle.Player.Heal(100);
        Assert.False(battle.CanExecutePlayerAction(BattleAction.Heal, FakeCatalog.SmallHealPotion));
        Assert.False(battle.CanExecutePlayerAction(BattleAction.UseItem, FakeCatalog.SmallHealPotion));
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.Heal, FakeCatalog.SmallHealPotion));
        Assert.Equal(1, battle.Player.Inventory.GetQuantity(FakeCatalog.SmallHealPotion));
        Assert.Equal(BattleState.PlayerTurn, battle.State);
    }

    [Fact]
    public void UseItem_UsesActualInventoryPotionAndRemovesOne()
    {
        var player = Player(50);
        player.Inventory.AddItem(FakeCatalog.SmallHealPotion.Clone(), 2);
        var battle = new Battle(player, Enemy());
        battle.Start();
        // Інший екземпляр із тим самим Id не може підмінити ефект предмета інвентарю.
        var forged = new Potion(1, "Інше зілля", "", 0, PotionEffect.Heal, 1000);
        Assert.Equal(25, battle.ExecutePlayerAction(BattleAction.UseItem, forged).HealedAmount);
        Assert.Equal(1, player.Inventory.GetQuantity(forged));
        battle.ExecuteEnemyAction();
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.UseItem, FakeCatalog.LargeHealPotion));
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.UseItem, FakeCatalog.IronSword));
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction((BattleAction)99));
        Assert.Equal(BattleState.PlayerTurn, battle.State);
    }

    [Fact]
    public void LethalAttack_FinishesBattleAndPreventsFurtherActions()
    {
        var battle = new Battle(Player(attack: 1000), Enemy());
        battle.Start();
        Assert.Equal(50, battle.ExecutePlayerAction(BattleAction.Attack).Damage);
        Assert.Equal(BattleState.Finished, battle.State);
        Assert.Equal(BattleResult.Victory, battle.CheckOutcome());
        Assert.Equal(0, battle.Enemy.Health);
        Assert.Throws<GameRuleException>(() => battle.ExecuteEnemyAction());
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.Attack));
        Assert.Throws<GameRuleException>(() => battle.NextTurn());
    }

    [Fact]
    public void Damage_HasMinimumOfOne()
    {
        var battle = new Battle(Player(), Enemy(defense: 100));
        battle.Start();
        Assert.Equal(1, battle.ExecutePlayerAction(BattleAction.Attack).Damage);
    }

    [Fact]
    public void EnemyCannotHealWithoutPotionInventory()
    {
        var enemy = Enemy(strategy: new HealingStrategy());
        enemy.TakeDamage(20);
        var battle = new Battle(Player(), enemy);
        battle.Start();
        battle.ExecutePlayerAction(BattleAction.Defend);
        Assert.Throws<GameRuleException>(() => battle.ExecuteEnemyAction());
        Assert.Equal(30, enemy.Health);
        Assert.Equal(BattleState.EnemyTurn, battle.State);
    }

    [Theory]
    [InlineData(StrategyType.Aggressive, 5, BattleAction.Attack)]
    [InlineData(StrategyType.Balanced, 5, BattleAction.Defend)]
    [InlineData(StrategyType.Balanced, 90, BattleAction.Attack)]
    [InlineData(StrategyType.Defensive, 5, BattleAction.Defend)]
    [InlineData(StrategyType.Defensive, 90, BattleAction.Defend)]
    public void Strategies_ChooseActionsFromHealth(StrategyType type, int health, BattleAction expected)
    {
        var enemy = new Enemy("Ворог", EnemyType.Goblin, 100, 10, 3, 0, 0,
            EnemyStrategyFactory.Create(type), health);
        Assert.Equal(expected, enemy.Strategy.ChooseAction(enemy, Player(40)));
    }

    [Fact]
    public void FinishBattle_RejectsUnfinishedBattleAndAwardsVictoryOnlyOnceEvenAfterSaveFailure()
    {
        var session = GameSession.StartNewGame("Герой", null, new FakeCatalog());
        var repository = new MemoryRepository { FailSave = true };
        new SaveManager(repository).Register(session);
        var events = new List<GameEventType>();
        session.Dispatcher.Subscribe(e => events.Add(e.Type));
        var battle = session.StartBattle(Enemy(health: 1, defense: 0));
        Assert.Throws<GameRuleException>(() => session.FinishBattle());
        var gold = session.Player.Gold;
        battle.ExecutePlayerAction(BattleAction.Attack);
        Assert.Throws<InvalidOperationException>(() => session.FinishBattle());
        Assert.Equal(GameState.Victory, session.CurrentState);
        repository.FailSave = false;
        session.FinishBattle();
        Assert.Equal(gold + 30, session.Player.Gold);
        Assert.Equal(20, session.Player.Experience);
        Assert.Equal(1, session.Player.Statistics.BattlesWon);
        Assert.Single(events, e => e == GameEventType.BattleWon);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(507)]
    public void Defeat_AppliesPenaltyRestoresHealthAndPublishesEventOnce(int gold)
    {
        var player = new Player("Герой", AppearanceSettings.Default, gold: gold);
        var dispatcher = new GameEventDispatcher();
        var session = new GameSession(Guid.NewGuid(), DateTime.UtcNow, GameState.City, player,
            new Acut.Core.Domain.Economy.City("Місто", player, dispatcher), dispatcher);
        var repository = new MemoryRepository();
        new SaveManager(repository).Register(session);
        var battle = session.StartBattle(Enemy(attack: 1000));
        battle.ExecutePlayerAction(BattleAction.Defend);
        battle.ExecuteEnemyAction();
        Assert.Equal(BattleResult.Defeat, battle.CheckOutcome());
        session.FinishBattle();
        session.FinishBattle();
        Assert.Equal(BattleResult.Defeat, battle.CheckOutcome());
        Assert.Equal(gold - gold / 10, player.Gold);
        Assert.Equal(player.MaxHealth, player.Health);
        Assert.Equal(1, player.Statistics.BattlesLost);
        Assert.Equal(0, player.Experience);
        Assert.Equal(GameState.Defeat, session.CurrentState);
    }

    [Fact]
    public void StartBattle_DeadEnemyDoesNotChangeSessionState()
    {
        var session = GameSession.StartNewGame("Герой", null, new FakeCatalog());
        var enemy = Enemy();
        enemy.TakeDamage(1000);
        Assert.Throws<GameRuleException>(() => session.StartBattle(enemy));
        Assert.Equal(GameState.City, session.CurrentState);
        Assert.Null(session.ActiveBattle);
    }

    private sealed class HealingStrategy : IEnemyStrategy
    {
        public BattleAction ChooseAction(Enemy self, Player target) => BattleAction.Heal;
    }

    private sealed class MemoryRepository : ISaveRepository
    {
        public bool FailSave { get; set; }
        public int SaveCount { get; private set; }
        public void Save(GameSession session)
        {
            if (FailSave) throw new InvalidOperationException("Помилка тестового сховища.");
            SaveCount++;
        }
        public GameSession? Load(Guid saveId) => null;
        public IReadOnlyList<SaveSlotInfo> ListSaves() => [];
        public void Delete(Guid saveId) { }
    }
}
