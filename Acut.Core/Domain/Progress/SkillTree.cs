using Acut.Core.Services;

namespace Acut.Core.Domain.Progress;

public sealed class SkillTree
{
    private readonly List<Skill> _skills = new();

    public IReadOnlyList<Skill> Skills => _skills;
    public int SkillPoints { get; set; }
    public GameEventDispatcher? Dispatcher { get; set; }
    public string? LastFailureReason { get; private set; }

    /// <summary>Заповнює дерево навичками з каталогу.</summary>
    public void Initialize(IEnumerable<Skill> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _skills.Clear();
        _skills.AddRange(skills);
    }

    /// <summary>Позначає навички вивченими при завантаженні збереження (очки не списуються).</summary>
    public void RestoreUnlocked(IEnumerable<int> skillIds)
    {
        var ids = skillIds.ToHashSet();
        foreach (var skill in _skills.Where(s => ids.Contains(s.Id)))
            skill.MarkUnlocked();
    }

    public bool IsUnlocked(int skillId) => _skills.Any(s => s.Id == skillId && s.IsUnlocked);

    /// <summary>Повертає причину неможливості вивчення або null, якщо навичку можна вивчити.</summary>
    public string? GetUnlockFailureReason(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (skill.IsUnlocked)
            return $"Навичка «{skill.Name}» вже вивчена.";

        var missingPrereqs = skill.Prerequisites.Where(p => !p.IsUnlocked).ToList();
        if (missingPrereqs.Count > 0)
        {
            var names = string.Join(", ", missingPrereqs.Select(p => $"«{p.Name}»"));
            return $"Не вивчено обов'язкові попередні навички: {names}.";
        }

        if (SkillPoints < skill.Cost)
            return $"Недостатньо очок навичок: потрібно {skill.Cost}, є {SkillPoints}.";

        return null;
    }

    public bool CanUnlock(Skill skill) => GetUnlockFailureReason(skill) is null;

    public bool CanUnlock(Skill skill, out string? reason)
    {
        reason = GetUnlockFailureReason(skill);
        return reason is null;
    }

    /// <summary>
    /// Вивчає навичку: перевіряє пререквізити й очки, списує Cost,
    /// позначає вивченою та публікує подію SkillUnlocked.
    /// </summary>
    public bool Unlock(Skill skill, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var reason = GetUnlockFailureReason(skill);
        if (reason is not null)
        {
            LastFailureReason = reason;
            return false;
        }

        SkillPoints -= skill.Cost;
        skill.MarkUnlocked();
        LastFailureReason = null;

        var targetDispatcher = dispatcher ?? Dispatcher;
        targetDispatcher?.Publish(new GameEvent(GameEventType.SkillUnlocked));
        return true;
    }

    public bool TryUnlock(Skill skill, out string? failureReason, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (!CanUnlock(skill, out failureReason))
        {
            LastFailureReason = failureReason;
            return false;
        }
        return Unlock(skill, dispatcher);
    }

    public void UnlockOrThrow(Skill skill, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var reason = GetUnlockFailureReason(skill);
        if (reason is not null)
            throw new GameRuleException(reason);

        Unlock(skill, dispatcher);
    }
}
