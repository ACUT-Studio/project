using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class ProgressAndEventsTests
{
    private static (Player Player, GameEventDispatcher Dispatcher) CreatePlayerWithProgress()
    {
        var player = new Player("Герой", AppearanceSettings.Default, gold: 100);
        var dispatcher = new GameEventDispatcher();
        dispatcher.Attach(player);
        return (player, dispatcher);
    }

    [Fact]
    public void SkillTree_UnlockWithPrerequisitesAndPoints_UnlocksAndPublishesEvent()
    {
        var (player, dispatcher) = CreatePlayerWithProgress();
        var s1 = new Skill(1, "Основи будівництва", "Базова навичка", 1);
        var s2 = new Skill(2, "Ремесла", "Відкриває майстерню", 2);
        s2.Prerequisites.Add(s1);

        player.SkillTree.Initialize([s1, s2]);
        player.SkillTree.SkillPoints = 3;

        var unlockedEvents = 0;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.SkillUnlocked) unlockedEvents++;
        });

        // Не можна вивчити s2 без пререквізиту s1
        Assert.False(player.SkillTree.CanUnlock(s2, out var prereqReason));
        Assert.NotNull(prereqReason);
        Assert.False(player.SkillTree.Unlock(s2));
        Assert.Equal(prereqReason, player.SkillTree.LastFailureReason);

        // Вивчаємо s1, потім s2
        Assert.True(player.SkillTree.TryUnlock(s1, out var failure1));
        Assert.Null(failure1);
        Assert.True(player.SkillTree.IsUnlocked(1));
        Assert.Equal(2, player.SkillTree.SkillPoints);

        player.SkillTree.UnlockOrThrow(s2);
        Assert.True(player.SkillTree.IsUnlocked(2));
        Assert.Equal(0, player.SkillTree.SkillPoints);
        Assert.Equal(2, unlockedEvents);

        // Повторне вивчення заборонене
        Assert.Throws<GameRuleException>(() => player.SkillTree.UnlockOrThrow(s2));
    }

    [Fact]
    public void SkillTree_InsufficientPoints_RejectsUnlockWithoutSideEffects()
    {
        var tree = new SkillTree { SkillPoints = 1 };
        var expensive = new Skill(1, "Торгівля", "", 2);
        tree.Initialize([expensive]);

        Assert.False(tree.CanUnlock(expensive));
        Assert.False(tree.Unlock(expensive));
        Assert.False(expensive.IsUnlocked);
        Assert.Equal(1, tree.SkillPoints);
        Assert.NotNull(tree.LastFailureReason);
    }

    [Fact]
    public void SkillTree_RestoreUnlocked_MarksSkillsWithoutSpendingPoints()
    {
        var tree = new SkillTree { SkillPoints = 5 };
        var s1 = new Skill(1, "Навичка 1", "", 2);
        var s2 = new Skill(2, "Навичка 2", "", 3);
        tree.Initialize([s1, s2]);

        tree.RestoreUnlocked([2]);

        Assert.False(tree.IsUnlocked(1));
        Assert.True(tree.IsUnlocked(2));
        Assert.Equal(5, tree.SkillPoints);
    }

    [Fact]
    public void QuestLog_Lifecycle_TracksProgressAndClaimsRewardExactlyOnce()
    {
        var (player, dispatcher) = CreatePlayerWithProgress();
        var quest = new Quest(1, "Містобудівник", "Збудуйте 2 будівлі", GameEventType.BuildingBuilt, 2, 150, 100);
        player.QuestLog.AddQuest(quest);
        player.QuestLog.AddQuest(quest); // Ідемпотентне додавання

        Assert.Single(player.QuestLog.AllQuests);
        Assert.Equal(QuestStatus.InProgress, quest.Status);

        // Не можна забрати нагороду до завершення
        Assert.Throws<InvalidOperationException>(() => player.QuestLog.ClaimReward(quest.Id));

        // Ігнорує події іншого типу
        dispatcher.Publish(new GameEvent(GameEventType.BattleWon, 5));
        Assert.Equal(0, quest.Progress);

        // Накопичує прогрес і обмежує його значенням Target
        dispatcher.Publish(new GameEvent(GameEventType.BuildingBuilt, 1));
        Assert.Equal(1, quest.Progress);
        Assert.Equal(QuestStatus.InProgress, quest.Status);

        dispatcher.Publish(new GameEvent(GameEventType.BuildingBuilt, 5));
        Assert.Equal(2, quest.Progress);
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.True(quest.IsCompleted());

        // Отримання нагороди підвищує золото та рівень (100 XP = 2 рівень)
        player.QuestLog.ClaimReward(quest.Id);

        Assert.Equal(QuestStatus.RewardClaimed, quest.Status);
        Assert.Empty(player.QuestLog.ActiveQuests);
        Assert.Single(player.QuestLog.CompletedQuests);
        Assert.Equal(250, player.Gold);
        Assert.Equal(2, player.Level);

        // Повторне отримання нагороди заборонене
        Assert.Throws<InvalidOperationException>(() => player.QuestLog.ClaimReward(quest));
        Assert.Throws<InvalidOperationException>(() => player.QuestLog.ClaimReward(999));
    }

    [Fact]
    public void AchievementTracker_UnlocksWhenCumulativeStatisticsReachThreshold()
    {
        var (player, dispatcher) = CreatePlayerWithProgress();
        var achievement = new Achievement(1, "Багатій", "Заробіть 100 золота", GameEventType.GoldEarned, 100);
        player.Achievements.Initialize([achievement]);

        dispatcher.Publish(new GameEvent(GameEventType.GoldEarned, 60));
        Assert.False(achievement.IsUnlocked);
        Assert.Null(achievement.UnlockedAt);

        dispatcher.Publish(new GameEvent(GameEventType.GoldEarned, 40));
        Assert.True(achievement.IsUnlocked);
        Assert.NotNull(achievement.UnlockedAt);

        var firstUnlockTime = achievement.UnlockedAt;
        achievement.Unlock();
        Assert.Equal(firstUnlockTime, achievement.UnlockedAt);
    }

    [Fact]
    public void PlayerStatistics_RecordsAllTrackedEventsAndPlayTime()
    {
        var stats = new PlayerStatistics();

        stats.Record(new GameEvent(GameEventType.BattleWon, 2));
        stats.Record(new GameEvent(GameEventType.BattleLost, 1));
        stats.Record(new GameEvent(GameEventType.BuildingBuilt, 3));
        stats.Record(new GameEvent(GameEventType.GoldEarned, 250));
        stats.Record(new GameEvent(GameEventType.PotionCrafted, 4));
        stats.Record(new GameEvent(GameEventType.SkillUnlocked, 1));

        stats.AddPlayTime(TimeSpan.FromSeconds(30));
        stats.AddPlayTime(TimeSpan.FromSeconds(-10)); // Від'ємний час ігнорується

        Assert.Equal(2, stats.GetTotal(GameEventType.BattleWon));
        Assert.Equal(1, stats.GetTotal(GameEventType.BattleLost));
        Assert.Equal(3, stats.GetTotal(GameEventType.BuildingBuilt));
        Assert.Equal(250, stats.GetTotal(GameEventType.GoldEarned));
        Assert.Equal(4, stats.GetTotal(GameEventType.PotionCrafted));
        Assert.Equal(0, stats.GetTotal(GameEventType.SkillUnlocked));
        Assert.Equal(TimeSpan.FromSeconds(30), stats.PlayTime);
    }

    [Fact]
    public void GameEventDispatcher_AttachIsIdempotent_DoesNotDuplicateSubscribers()
    {
        var (player, dispatcher) = CreatePlayerWithProgress();
        dispatcher.Attach(player); // Повторний виклик

        dispatcher.Publish(new GameEvent(GameEventType.BattleWon, 1));

        Assert.Equal(1, player.Statistics.BattlesWon);
    }
}