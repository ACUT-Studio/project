using Acut.Core.Domain.Combat;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Data;

public sealed class BattlePersistenceTests
{
    [Fact]
    public void ActiveBattle_RestoresTurnHealthAndDefensiveStance()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Герой", null, db.Catalog);
        var battle = session.StartBattle(db.Catalog.GetEnemies().First().CreateEnemy());
        battle.ExecutePlayerAction(BattleAction.Defend);
        db.Saves.Save(session);
        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(GameState.Battle, loaded.CurrentState);
        Assert.Equal(BattleState.EnemyTurn, loaded.ActiveBattle!.State);
        Assert.True(loaded.ActiveBattle.PlayerDefending);
        Assert.Equal(battle.Enemy.Health, loaded.ActiveBattle.Enemy.Health);
        Assert.Equal(battle.ExecuteEnemyAction(), loaded.ActiveBattle.ExecuteEnemyAction());
        Assert.Equal(session.Player.Health, loaded.Player.Health);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FinishedBattle_ReloadDoesNotRepeatSettlementAndReturningToCityRemovesBattle(bool victory)
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Герой", null, db.Catalog);
        var saves = new SaveManager(db.Saves);
        saves.Register(session);
        var battle = session.StartBattle(db.Catalog.GetEnemies().First().CreateEnemy());
        if (victory) battle.Enemy.TakeDamage(battle.Enemy.MaxHealth);
        else session.Player.TakeDamage(session.Player.MaxHealth);
        battle.NextTurn();
        session.FinishBattle();
        var loaded = saves.LoadGame(session.SessionId)!;
        Assert.Equal(victory ? GameState.Victory : GameState.Defeat, loaded.CurrentState);
        Assert.Equal(BattleState.Finished, loaded.ActiveBattle!.State);
        Assert.Equal(victory ? BattleResult.Victory : BattleResult.Defeat, loaded.ActiveBattle.CheckOutcome());
        var gold = loaded.Player.Gold;
        var xp = loaded.Player.Experience;
        loaded.FinishBattle();
        Assert.Equal(gold, loaded.Player.Gold);
        Assert.Equal(xp, loaded.Player.Experience);
        Assert.Equal(victory ? 1 : 0, loaded.Player.Statistics.BattlesWon);
        Assert.Equal(victory ? 0 : 1, loaded.Player.Statistics.BattlesLost);
        loaded.ChangeState(GameState.City);
        saves.AutoSave();
        Assert.Null(saves.LoadGame(session.SessionId)!.ActiveBattle);
        Assert.Equal(0, db.Count("ActiveBattle"));
    }
}
