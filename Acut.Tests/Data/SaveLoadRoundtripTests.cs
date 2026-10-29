using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
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
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);
        var hut = db.Catalog.GetBuildingTypes().First(t => t.Id == 1);

        // Act
        session.City.BuildBuilding(hut);
        session.City.BuildBuilding(hut);
        session.Tick(TimeSpan.FromSeconds(5));
        session.Save();

        var loaded = db.Saves.Load(session.SessionId);

        // Assert
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
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);
        var hut = db.Catalog.GetBuildingTypes().First(t => t.Id == 1);

        // Act
        session.City.BuildBuilding(hut);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        // Assert
        var firstQuest = loaded.Player.QuestLog.AllQuests.First(q => q.Id == 1);
        Assert.Equal(QuestStatus.Completed, firstQuest.Status);
        Assert.Equal(firstQuest.Target, firstQuest.Progress);
    }

    [Fact]
    public void SaveAndLoad_RestoresUpgradedBuilding_UnlockedSkill_AndClaimedQuest()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);
        var hutType = db.Catalog.GetBuildingTypes().First(t => t.Id == 1);
        var hut = session.City.BuildBuilding(hutType);

        // Act
        session.City.UpgradeBuilding(hut); // Level 2, Active

        var basicSkill = session.Player.SkillTree.Skills.First(s => s.Id == 1);
        session.Player.SkillTree.Unlock(basicSkill);

        var quest = session.Player.QuestLog.AllQuests.First(q => q.Id == 1);
        session.Player.QuestLog.ClaimReward(quest, session.Player);

        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        // Assert
        var loadedHut = loaded.City.GetBuildings().Single();
        Assert.Equal(2, loadedHut.Level);
        Assert.Equal(BuildingStatus.Active, loadedHut.Status);

        Assert.True(loaded.Player.SkillTree.IsUnlocked(1));
        var loadedQuest = loaded.Player.QuestLog.AllQuests.First(q => q.Id == 1);
        Assert.Equal(QuestStatus.RewardClaimed, loadedQuest.Status);
    }

    [Fact]
    public void SaveTwice_UpdatesSameSave_NoDuplicates()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);

        // Act
        session.Save();
        session.City.BuildBuilding(db.Catalog.GetBuildingTypes().First(t => t.Id == 1));
        session.Save();

        // Assert
        Assert.Equal(1, db.Count("GameSave"));
        Assert.Equal(1, db.Count("Player"));
        Assert.Equal(1, db.Count("City"));
        Assert.Equal(1, db.Count("CityBuilding"));
    }

    [Fact]
    public void SaveAndLoad_ActiveBattle_IsRestored()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);
        var goblin = db.Catalog.GetEnemies().First();
        var enemy = goblin.CreateEnemy();

        // Act
        enemy.TakeDamage(10);
        session.StartBattle(enemy);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        // Assert
        Assert.Equal(GameState.Battle, loaded.CurrentState);
        Assert.NotNull(loaded.ActiveBattle);
        Assert.Equal(enemy.Health, loaded.ActiveBattle.Enemy.Health);
        Assert.Equal(goblin.Name, loaded.ActiveBattle.Enemy.Name);
        Assert.Equal(BattleState.PlayerTurn, loaded.ActiveBattle.State);
    }

    [Fact]
    public void Load_UnknownId_ReturnsNull()
    {
        // Arrange
        using var db = new TestDatabase();

        // Act & Assert
        Assert.Null(db.Saves.Load(Guid.NewGuid()));
    }

    [Fact]
    public void ListSaves_ReturnsNewestFirst()
    {
        // Arrange
        using var db = new TestDatabase();
        var first = NewGame(db);
        first.Save();
        var second = NewGame(db);

        // Act
        second.Save();
        var saves = db.Saves.ListSaves();

        // Assert
        Assert.Equal(2, saves.Count);
        Assert.Equal(second.SessionId, saves[0].SaveId);
    }

    [Fact]
    public void Delete_RemovesWholeSaveWithItsState()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);
        session.City.BuildBuilding(db.Catalog.GetBuildingTypes().First(t => t.Id == 1));
        session.Save();

        // Act
        db.Saves.Delete(session.SessionId);

        // Assert
        Assert.Empty(db.Saves.ListSaves());
        Assert.Equal(0, db.Count("Player"));
        Assert.Equal(0, db.Count("City"));
        Assert.Equal(0, db.Count("CityBuilding"));
        Assert.Equal(0, db.Count("PlayerQuest"));
        Assert.Equal(0, db.Count("SaveStock"));
    }

    [Fact]
    public void SaveAndLoad_PausedState_IsRestoredCorrectly()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);

        // Act
        session.ChangeState(GameState.Paused);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        // Assert
        Assert.Equal(GameState.Paused, loaded.CurrentState);
    }

    [Fact]
    public void SaveAndLoad_PreservesPlayerHealthAndStats()
    {
        // Arrange
        using var db = new TestDatabase();
        var session = NewGame(db);

        // Act
        session.Player.TakeDamage(15);
        session.Player.GainExperience(25);
        session.Save();

        var loaded = db.Saves.Load(session.SessionId)!;

        // Assert
        Assert.Equal(session.Player.Health, loaded.Player.Health);
        Assert.Equal(session.Player.Experience, loaded.Player.Experience);
        Assert.Equal(session.Player.Level, loaded.Player.Level);
    }

    [Fact]
    public void Delete_NonExistentSave_DoesNotThrow()
    {
        // Arrange
        using var db = new TestDatabase();
        var randomId = Guid.NewGuid();

        // Act & Assert (перевіряємо, що видалення неіснуючого збереження безпечне)
        var exception = Record.Exception(() => db.Saves.Delete(randomId));
        Assert.Null(exception);
    }

}