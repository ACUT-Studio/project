namespace Acut.Core.Domain.Progress;

public sealed class Achievement
{
    public Achievement(int id, string title, string condition, GameEventType eventType, int threshold)
    {
        Id = id;
        Title = title;
        Condition = condition;
        EventType = eventType;
        Threshold = threshold;
    }

    public int Id { get; }
    public string Title { get; }
    public string Condition { get; }
    public GameEventType EventType { get; }
    public int Threshold { get; }
    public bool IsUnlocked { get; private set; }
    public DateTime? UnlockedAt { get; private set; }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        UnlockedAt = DateTime.UtcNow;
    }

    /// <summary>Відновлює розблоковане досягнення із збереження.</summary>
    public void Restore(DateTime unlockedAt)
    {
        IsUnlocked = true;
        UnlockedAt = unlockedAt;
    }
}
