using Acut.Core.Domain;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class GameSessionTests
{
    [Fact]
    public void StartNewGame_ValidName_EndsInCityWithStartingResources()
    {
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        Assert.Equal(GameState.City, session.CurrentState);
        Assert.Equal(GameConfig.StartingGold, session.Player.Gold);
        Assert.Equal(GameConfig.StartingSkillPoints, session.Player.SkillPoints);
        Assert.Single(session.Player.QuestLog.ActiveQuests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartNewGame_EmptyName_ThrowsGameRuleException(string name)
    {
        Assert.Throws<GameRuleException>(() => GameSession.StartNewGame(name, null, new FakeCatalog()));
    }

    [Fact]
    public void StartNewGame_TooLongName_ThrowsGameRuleException()
    {
        var name = new string('а', GameConfig.MaxPlayerNameLength + 1);

        Assert.Throws<GameRuleException>(() => GameSession.StartNewGame(name, null, new FakeCatalog()));
    }

    [Fact]
    public void ChangeState_InvalidTransition_Throws()
    {
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        Assert.Throws<InvalidOperationException>(() => session.ChangeState(GameState.Victory));
    }

    [Fact]
    public void ChangeState_PauseAndBack_Works()
    {
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        session.ChangeState(GameState.Paused);
        session.ChangeState(GameState.City);

        Assert.Equal(GameState.City, session.CurrentState);
    }

    [Fact]
    public void Tick_WhilePaused_DoesNotPayIncome()
    {
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());
        session.City.BuildBuilding(FakeCatalog.Hut);
        session.ChangeState(GameState.Paused);
        var gold = session.Player.Gold;

        session.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(gold, session.Player.Gold);
    }

    [Fact]
    public void BuildingQuest_CompletesAfterFirstBuilding()
    {
        var session = GameSession.StartNewGame("Марк", null, new FakeCatalog());

        session.City.BuildBuilding(FakeCatalog.Hut);

        var quest = session.Player.QuestLog.ActiveQuests.Single();
        Assert.Equal(QuestStatus.Completed, quest.Status);
    }
}
