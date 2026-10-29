using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class GameSessionTests
{

    [Fact]
    public void StartNewGame_ValidName_EndsInCityWithStartingResources()
    {
        // Arrange
        const string playerName = "Марк";

        // Act
        var session = GameSession.StartNewGame(playerName, null, new FakeCatalog());

        // Assert
        Assert.Equal(GameState.City, session.CurrentState);
        Assert.Equal(playerName, session.Player.Name);
        Assert.Equal(GameConfig.StartingGold, session.Player.Gold);
        Assert.Equal(GameConfig.StartingSkillPoints, session.Player.SkillPoints);
        Assert.Single(session.Player.QuestLog.ActiveQuests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartNewGame_EmptyName_ThrowsGameRuleException(string name)
    {
        // Act & Assert
        Assert.Throws<GameRuleException>(() => GameSession.StartNewGame(name, null, new FakeCatalog()));
    }

    [Fact]
    public void StartNewGame_TooLongName_ThrowsGameRuleException()
    {
        // Arrange
        var name = new string('а', GameConfig.MaxPlayerNameLength + 1);

        // Act & Assert
        Assert.Throws<GameRuleException>(() => GameSession.StartNewGame(name, null, new FakeCatalog()));
    }

    [Fact]
    public void ChangeState_InvalidTransition_Throws()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => session.ChangeState(GameState.Victory));
    }

    [Fact]
    public void ChangeState_PauseAndBack_Works()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        // Act
        session.ChangeState(GameState.Paused);
        session.ChangeState(GameState.City);

        // Assert
        Assert.Equal(GameState.City, session.CurrentState);
    }

    [Fact]
    public void Tick_WhilePaused_DoesNotPayIncome()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());
        session.City.BuildBuilding(FakeCatalog.Hut);

        session.ChangeState(GameState.Paused);
        var goldBeforeTick = session.Player.Gold;

        // Act
        session.Tick(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(goldBeforeTick, session.Player.Gold);
    }

    [Fact]
    public void BuildingQuest_CompletesAfterFirstBuilding()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        // Act
        session.City.BuildBuilding(FakeCatalog.Hut);

        // Assert
        var quest = session.Player.QuestLog.ActiveQuests.Single();
        Assert.Equal(QuestStatus.Completed, quest.Status);
    }

 

    [Fact]
    public void StartNewGame_WithCustomAppearance_AppliesSuccessfully()
    {
        // Arrange
        var appearance = AppearanceSettings.Default; 

        // Act
        var session = GameSession.StartNewGame("Марк", appearance, new FakeCatalog());

        // Assert
        Assert.NotNull(session.Player.Appearance);
        Assert.Equal(GameState.City, session.CurrentState);
    }

    [Fact]
    public void ChangeState_FromPausedBackToPaused_ThrowsOrMaintainsState()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());
        session.ChangeState(GameState.Paused);

        // Act & Assert 
        Assert.Throws<InvalidOperationException>(() => session.ChangeState(GameState.Paused));
    }

    [Fact]
    public void Tick_ResumedAfterPause_ContinuesPayingIncome()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());
        session.City.BuildBuilding(FakeCatalog.Hut);
        var income = FakeCatalog.Hut.IncomePerTick;

        // Ставимо на паузу (тіки не мають працювати)
        session.ChangeState(GameState.Paused);
        session.Tick(TimeSpan.FromSeconds(1));
        var goldAfterPausedTick = session.Player.Gold;

        // Act — повертаємось у гру та робимо тік знову
        session.ChangeState(GameState.City);
        session.Tick(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(goldAfterPausedTick, session.Player.Gold - income);
    }

    [Fact]
    public void BuildingQuest_MovesToCompletedOrArchived_AfterFulfillment()
    {
        // Arrange
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());
        var quest = session.Player.QuestLog.ActiveQuests.Single();

        // Act
        session.City.BuildBuilding(FakeCatalog.Hut);

        // Assert
        Assert.Equal(QuestStatus.Completed, quest.Status);
        
    }

    [Fact]
    public void StartNewGame_InitializesWorldCatalogCorrectly()
    {
        // Arrange & Act
        var catalog = new FakeCatalog();
        var session = GameSession.StartNewGame("Марк", null, catalog);

        // Assert
        Assert.NotNull(session);
        Assert.NotNull(session.City);
        Assert.NotNull(session.Player);
    }

   
}