using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

/// <summary>Показує вже нарахований результат; повернення не видає нагород повторно.</summary>
public sealed class VictoryViewModel : ViewModelBase
{
    private readonly GameSession _session;
    private readonly SaveManager _saves;
    private readonly NavigationService _navigation;
    private string _statusMessage = string.Empty;

    public VictoryViewModel(NavigationService navigation, SaveManager saves)
    {
        _navigation = navigation;
        _saves = saves;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");
        IsVictory = _session.CurrentState == GameState.Victory;
        if (_session.CurrentState is not (GameState.Victory or GameState.Defeat))
            throw new InvalidOperationException("Результату бою немає.");
        EnemyName = _session.ActiveBattle?.Enemy.Name ?? string.Empty;
        RewardGold = IsVictory ? $"+{_session.ActiveBattle?.Enemy.RewardGold ?? 0} золота" :
            _session.LastDefeatPenalty is int penalty ? $"−{penalty} золота" : "Штраф до золота застосовано";
        RewardExperience = IsVictory ? $"+{_session.ActiveBattle?.Enemy.RewardExperience ?? 0} досвіду" : "Здоров'я повністю відновлено";
        BackCommand = new RelayCommand(ReturnToCity);
    }

    public bool IsVictory { get; }
    public string Title => IsVictory ? "ПЕРЕМОГА" : "ПОРАЗКА";
    public string EnemyName { get; }
    public string RewardGold { get; }
    public string RewardExperience { get; }
    public string Description => IsVictory ? "ВАШІ НАГОРОДИ" : "Можна повернутися до міста й підготуватися до нового бою.";
    public int Level => _session.Player.Level;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand BackCommand { get; }

    private void ReturnToCity()
    {
        try
        {
            if (_session.CurrentState is GameState.Victory or GameState.Defeat) _session.ChangeState(GameState.City);
            _saves.AutoSave();
            _navigation.NavigateTo(GameState.City);
        }
        catch (Exception ex) { StatusMessage = $"Не вдалося зберегти гру: {ex.Message}. Спробуйте ще раз."; }
    }
}
