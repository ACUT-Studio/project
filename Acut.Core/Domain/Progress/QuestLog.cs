using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Progress;

public sealed class QuestLog
{
    private readonly List<Quest> _quests = new();

    public Player? Player { get; set; }

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

    /// <summary>
    /// Отримує нагороду за виконане завдання: нараховує золото й досвід (з можливим підвищенням рівня).
    /// Нагороду можна отримати лише один раз і лише за виконане завдання.
    /// </summary>
    /// <exception cref="ArgumentNullException">Завдання або гравця не передано.</exception>
    /// <exception cref="InvalidOperationException">Завдання не виконано або нагороду вже отримано.</exception>
    public void ClaimReward(Quest quest, Player player)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(player);

        if (!_quests.Contains(quest))
            throw new InvalidOperationException($"Завдання «{quest.Title}» не знайдено в журналі.");

        if (quest.Status == QuestStatus.RewardClaimed)
            throw new InvalidOperationException($"Нагороду за завдання «{quest.Title}» вже отримано.");

        if (quest.Status != QuestStatus.Completed)
            throw new InvalidOperationException($"Завдання «{quest.Title}» ще не виконано.");

        quest.MarkRewardClaimed();

        if (quest.RewardGold > 0)
            player.AddGold(quest.RewardGold);

        if (quest.RewardExperience > 0)
            player.GainExperience(quest.RewardExperience);
    }

    public void ClaimReward(Quest quest) =>
        ClaimReward(quest, Player ?? throw new InvalidOperationException("Гравця не призначено для журналу завдань."));

    public void ClaimReward(int questId, Player player)
    {
        var quest = _quests.FirstOrDefault(q => q.Id == questId)
            ?? throw new InvalidOperationException($"Завдання з ID {questId} не знайдено в журналі.");
        ClaimReward(quest, player);
    }

    public void ClaimReward(int questId) =>
        ClaimReward(questId, Player ?? throw new InvalidOperationException("Гравця не призначено для журналу завдань."));
}
