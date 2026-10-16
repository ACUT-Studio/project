namespace Acut.Core.Domain.Progress;

public sealed class SkillTree
{
    private readonly List<Skill> _skills = new();

    public IReadOnlyList<Skill> Skills => _skills;
    public int SkillPoints { get; set; }

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

    public bool CanUnlock(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return !skill.IsUnlocked
               && SkillPoints >= skill.Cost
               && skill.Prerequisites.All(p => p.IsUnlocked);
    }

    public bool Unlock(Skill skill)
    {
        if (!CanUnlock(skill)) return false;
        SkillPoints -= skill.Cost;
        skill.MarkUnlocked();
        return true;
    }
}
