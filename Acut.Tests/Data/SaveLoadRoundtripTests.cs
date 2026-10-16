using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Data;

/// <summary>Наскрізна перевірка етапу 5: нова гра → будівництво → збереження → завантаження.</summary>
public class SaveLoadRoundtripTests
{
    private static GameSession NewGame(TestDatabase db)
    {
        var session = GameSession.StartNewGame("Марк", null, db.Catalog);
        new SaveManager(db.Saves).Register(session);
        return session;
    }

    [Fact]
    public void SaveAndLoad_RestoresPlayerCityAndProgress()
    {
        using var db = new TestDatabase();
        var session = NewGame(db);
        var hut = db.Catalog.GetBuildingTypes().First(t => t.Id == 1);
        session.City.BuildBuilding(hut);
        session.City.BuildBuilding(hut);
        session.Tick(TimeSpan.FromSeconds(5));
        session.Save();

        var loaded = db.Saves.Load(session.SessionId);

        Assert.NotNull(loaded);
        Assert.Equal(session.SessionId, loaded.SessionId);
        Assert.Equal(GameState.City, loaded.CurrentState);
        Assert.Equal(session.Player.Name, loaded.Player.Name);
        Assert.Equal(session.Player.Gold, loaded.Player.Gold);
        Assert.Equal(session.Player.SkillPoints, loaded.Player.SkillPoints);
        Assert.Equal(2, loaded.City.GetBuildings().Count);
        Assert.Equal(session.City.GetBuildings()[0].Id, loaded.City.GetBuildings()[0].Id);
        Assert.Equal(session.City.Population, loaded.City.Population);
        Assert.Equal(2, loaded.Player.Statistics.BuildingsBuilt);
        Assert.Equal(session.Player.Statistics.PlayTime, loaded.Player.Statistics.PlayTime);
    }

    [Fact]
    public void SaveAndLoad_RestoresQuestAndAchievementProgress()
    {
        using var db = new TestDatabase();
        var session = NewGame(db);
        var hut = db.Catalog.GetBuildingTypes().First(t => t.Id == 1);
        session.City.BuildBuilding(hut);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        var firstQuest = loaded.Player.QuestLog.AllQuests.First(q => q.Id == 1);
        Assert.Equal(QuestStatus.Completed, firstQuest.Status);
        Assert.Equal(firstQuest.Target, firstQuest.Progress);
    }

    [Fact]
    public void SaveTwice_UpdatesSameSave_NoDuplicates()
    {
        using var db = new TestDatabase();
        var session = NewGame(db);

        session.Save();
        session.City.BuildBuilding(db.Catalog.GetBuildingTypes().First(t => t.Id == 1));
        session.Save();

        Assert.Equal(1, db.Count("GameSave"));
        Assert.Equal(1, db.Count("Player"));
        Assert.Equal(1, db.Count("City"));
        Assert.Equal(1, db.Count("CityBuilding"));
    }

    [Fact]
    public void SaveAndLoad_ActiveBattle_IsRestored()
    {
        using var db = new TestDatabase();
        var session = NewGame(db);
        var goblin = db.Catalog.GetEnemies().First();
        var enemy = goblin.CreateEnemy();
        enemy.TakeDamage(10);
        session.StartBattle(enemy);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        Assert.Equal(GameState.Battle, loaded.CurrentState);
        Assert.NotNull(loaded.ActiveBattle);
        Assert.Equal(enemy.Health, loaded.ActiveBattle.Enemy.Health);
        Assert.Equal(goblin.Name, loaded.ActiveBattle.Enemy.Name);
        Assert.Equal(BattleState.PlayerTurn, loaded.ActiveBattle.State);
    }

    [Fact]
    public void Load_UnknownId_ReturnsNull()
    {
        using var db = new TestDatabase();

        Assert.Null(db.Saves.Load(Guid.NewGuid()));
    }

    [Fact]
    public void ListSaves_ReturnsNewestFirst()
    {
        using var db = new TestDatabase();
        var first = NewGame(db);
        first.Save();
        var second = NewGame(db);
        second.Save();

        var saves = db.Saves.ListSaves();

        Assert.Equal(2, saves.Count);
        Assert.Equal(second.SessionId, saves[0].SaveId);
    }

    [Fact]
    public void Delete_RemovesWholeSaveWithItsState()
    {
        using var db = new TestDatabase();
        var session = NewGame(db);
        session.City.BuildBuilding(db.Catalog.GetBuildingTypes().First(t => t.Id == 1));
        session.Save();

        db.Saves.Delete(session.SessionId);

        Assert.Empty(db.Saves.ListSaves());
        Assert.Equal(0, db.Count("Player"));
        Assert.Equal(0, db.Count("City"));
        Assert.Equal(0, db.Count("CityBuilding"));
        Assert.Equal(0, db.Count("PlayerQuest"));
        Assert.Equal(0, db.Count("SaveStock"));
    }
}
