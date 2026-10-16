namespace Acut.Core.Domain.Progress;

public sealed class Quest
{
    public Quest(int id, string title, string description, GameEventType eventType, int target, int rewardGold, int rewardExperience)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(target, 1);
        Id = id;
        Title = title;
        Description = description ?? string.Empty;
        EventType = eventType;
        Target = target;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
    }

    public int Id { get; }
    public string Title { get; }
    public string Description { get; }
    public GameEventType EventType { get; }
    public QuestStatus Status { get; private set; } = QuestStatus.NotStarted;
    public int Progress { get; private set; }
    public int Target { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }

    public bool IsCompleted() => Status is QuestStatus.Completed or QuestStatus.RewardClaimed;

    internal void Start()
    {
        if (Status == QuestStatus.NotStarted) Status = QuestStatus.InProgress;
    }

    internal void AddProgress(int value)
    {
        if (Status != QuestStatus.InProgress) return;
        Progress = Math.Min(Target, Progress + Math.Max(0, value));
        if (Progress >= Target) Status = QuestStatus.Completed;
    }

    internal void MarkRewardClaimed()
    {
        if (Status != QuestStatus.Completed)
            throw new InvalidOperationException("Нагороду можна отримати лише за виконане завдання.");
        Status = QuestStatus.RewardClaimed;
    }

    /// <summary>Відновлює стан із збереження.</summary>
    public void Restore(QuestStatus status, int progress)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, Target);
    }
}
