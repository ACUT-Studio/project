using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Progress;

public sealed class QuestLog
{
    private readonly List<Quest> _quests = new();

    /// <summary>Усі завдання журналу (для збереження).</summary>
    public IReadOnlyList<Quest> AllQuests => _quests;

    /// <summary>Виконуються або виконані, але нагороду ще не отримано.</summary>
    public IReadOnlyList<Quest> ActiveQuests =>
        _quests.Where(q => q.Status is QuestStatus.InProgress or QuestStatus.Completed).ToList();

    public IReadOnlyList<Quest> CompletedQuests =>
        _quests.Where(q => q.Status == QuestStatus.RewardClaimed).ToList();

    public void AddQuest(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (_quests.Any(q => q.Id == quest.Id)) return;
        quest.Start();
        _quests.Add(quest);
    }

    /// <summary>Додає завдання зі збереженим станом (без запуску).</summary>
    public void Restore(IEnumerable<Quest> quests)
    {
        _quests.Clear();
        _quests.AddRange(quests);
    }

    public void UpdateProgress(GameEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        foreach (var quest in _quests.Where(q => q.Status == QuestStatus.InProgress && q.EventType == e.Type))
            quest.AddProgress(e.Value);
    }

    public void ClaimReward(Quest quest, Player player)
    {
        // TODO(Етап 6): нарахувати RewardGold і RewardExperience, quest.MarkRewardClaimed().
        throw new NotImplementedException("Етап 6: QuestLog.ClaimReward");
    }
}
