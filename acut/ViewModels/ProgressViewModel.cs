using System.Collections.ObjectModel;
using System.Windows.Input;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;

namespace acut.ViewModels;

public sealed record SkillRow(Skill Skill, string State, string Hint)
{
    public string Prerequisites => Skill.Prerequisites.Count == 0 ? "Немає" :
        string.Join(", ", Skill.Prerequisites.Select(s => $"{s.Name} ({(s.IsUnlocked ? "вивчена" : "не вивчена")})"));
}

public sealed record QuestRow(Quest Quest, int Progress, string State);
public sealed record AchievementRow(Achievement Achievement, string State, string Date);

/// <summary>Відображає реальний прогрес сесії; правила вивчення й нагород залишаються в Core.</summary>
public sealed class ProgressViewModel : ViewModelBase
{
    private readonly GameSession _session;
    private readonly Action _refreshCity;
    private string _statusMessage = string.Empty;

    public ProgressViewModel(GameSession session, Action refreshCity)
    {
        _session = session;
        _refreshCity = refreshCity;
        LearnCommand = new RelayCommand(p => Learn(p as SkillRow), p => CanLearn(p as SkillRow));
        ClaimRewardCommand = new RelayCommand(p => Claim(p as QuestRow), p => CanClaim(p as QuestRow));
        Refresh();
    }

    public ObservableCollection<SkillRow> Skills { get; } = new();
    public ObservableCollection<QuestRow> Quests { get; } = new();
    public ObservableCollection<AchievementRow> Achievements { get; } = new();
    public int SkillPoints => _session.Player.SkillTree.SkillPoints;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand LearnCommand { get; }
    public RelayCommand ClaimRewardCommand { get; }

    private bool CanLearn(SkillRow? row) => _session.CurrentState == GameState.City && row is not null &&
        _session.Player.SkillTree.Skills.Contains(row.Skill) && _session.Player.SkillTree.CanUnlock(row.Skill);

    private bool CanClaim(QuestRow? row) => _session.CurrentState == GameState.City && row is not null &&
        _session.Player.QuestLog.AllQuests.Contains(row.Quest) && row.Quest.Status == QuestStatus.Completed;

    private void Learn(SkillRow? row)
    {
        if (!CanLearn(row)) return;
        StatusMessage = _session.Player.SkillTree.Unlock(row!.Skill)
            ? $"Вивчено: {row.Skill.Name}." : _session.Player.SkillTree.LastFailureReason ?? string.Empty;
        _refreshCity();
    }

    private void Claim(QuestRow? row)
    {
        if (!CanClaim(row)) return;
        _session.Player.QuestLog.ClaimReward(row!.Quest, _session.Player);
        StatusMessage = $"Отримано нагороду: {row.Quest.Title}.";
        _refreshCity();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(SkillPoints));
        Synchronize(Skills, _session.Player.SkillTree.Skills.Select(s => new SkillRow(s,
            s.IsUnlocked ? "Вивчена" : _session.Player.SkillTree.CanUnlock(s) ? "Доступна" : "Заблокована",
            _session.Player.SkillTree.GetUnlockFailureReason(s) ?? "Можна вивчити")));
        Synchronize(Quests, _session.Player.QuestLog.AllQuests.Select(q => new QuestRow(q, q.Progress, q.Status switch
        {
            QuestStatus.Completed => "Виконано",
            QuestStatus.RewardClaimed => "Нагороду отримано",
            QuestStatus.InProgress => "Виконується",
            _ => "Не розпочато"
        })));
        Synchronize(Achievements, _session.Player.Achievements.Achievements.Select(a => new AchievementRow(a,
            a.IsUnlocked ? "Здобуто" : "Заблоковано",
            a.UnlockedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Ще не здобуто")));
        CommandManager.InvalidateRequerySuggested();
    }

    private static void Synchronize<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var index = 0;
        foreach (var value in source)
        {
            if (index == target.Count) target.Add(value);
            else if (!EqualityComparer<T>.Default.Equals(target[index], value)) target[index] = value;
            index++;
        }
        while (target.Count > index) target.RemoveAt(target.Count - 1);
    }
}
