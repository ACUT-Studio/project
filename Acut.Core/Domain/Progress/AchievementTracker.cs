namespace Acut.Core.Domain.Progress;

public sealed class AchievementTracker
{
    private readonly PlayerStatistics _statistics;
    private readonly List<Achievement> _achievements = new();

    public AchievementTracker(PlayerStatistics statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    public IReadOnlyList<Achievement> Achievements => _achievements;

    public void Initialize(IEnumerable<Achievement> achievements)
    {
        ArgumentNullException.ThrowIfNull(achievements);
        _achievements.Clear();
        _achievements.AddRange(achievements);
    }

    /// <summary>Розблоковує досягнення, поріг яких досягнуто за накопиченою статистикою.</summary>
    public void CheckTriggers(GameEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var total = _statistics.GetTotal(e.Type);
        foreach (var a in _achievements.Where(a => !a.IsUnlocked && a.EventType == e.Type && total >= a.Threshold))
            a.Unlock();
    }
}
