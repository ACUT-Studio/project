using System.Collections.ObjectModel;
using System.Windows.Input;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

/// <summary>Відображає поточний бій і передає всі правила доменній моделі.</summary>
public sealed class BattleViewModel : ViewModelBase
{
    private readonly GameSession _session;
    private readonly NavigationService _navigation;
    private ItemStack? _selectedItem;
    private string _statusMessage = string.Empty;

    public BattleViewModel(NavigationService navigation, SaveManager saves)
    {
        _navigation = navigation;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");
        Battle = _session.ActiveBattle ?? throw new InvalidOperationException("Активного бою немає.");
        AttackCommand = ActionCommand(BattleAction.Attack);
        DefendCommand = ActionCommand(BattleAction.Defend);
        UseItemCommand = ActionCommand(BattleAction.UseItem);
        ContinueCommand = new RelayCommand(Finish, () => Battle.State == BattleState.Finished);
        Refresh();
        // Збереження може припадати на початок ходу ворога.
        if (Battle.State == BattleState.EnemyTurn) ResolveEnemy();
    }

    public Battle Battle { get; }
    public string PlayerName => Battle.Player.Name;
    public AppearanceSettings Appearance => Battle.Player.Appearance;
    public string EnemyName => Battle.Enemy.Name;
    public int PlayerHealth => Battle.Player.Health;
    public int PlayerMaxHealth => Battle.Player.MaxHealth;
    public int EnemyHealth => Battle.Enemy.Health;
    public int EnemyMaxHealth => Battle.Enemy.MaxHealth;
    public int PlayerLevel => Battle.Player.Level;
    public string TurnText => Battle.State == BattleState.Finished ? "Бій завершено" : $"Хід {Battle.TurnNumber} · ваш вибір";
    public ObservableCollection<string> Log { get; } = new();
    public ObservableCollection<ItemStack> Items { get; } = new();
    public ItemStack? SelectedItem
    {
        get => _selectedItem;
        set { if (SetProperty(ref _selectedItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand AttackCommand { get; }
    public RelayCommand DefendCommand { get; }
    public RelayCommand UseItemCommand { get; }
    public RelayCommand ContinueCommand { get; }

    private RelayCommand ActionCommand(BattleAction action) => new(() => Act(action),
        () => _session.CurrentState == GameState.Battle && Battle.CanExecutePlayerAction(action, SelectedItem?.Item));

    private void Act(BattleAction action)
    {
        if (!Battle.CanExecutePlayerAction(action, SelectedItem?.Item) || _session.CurrentState != GameState.Battle) return;
        Log.Insert(0, $"Хід {Battle.TurnNumber} · {Battle.ExecutePlayerAction(action, SelectedItem?.Item).Message}");
        ResolveEnemy();
        if (Battle.State == BattleState.Finished) Finish();
    }

    private void ResolveEnemy()
    {
        if (Battle.State == BattleState.EnemyTurn)
            Log.Insert(0, $"Хід {Battle.TurnNumber} · {Battle.ExecuteEnemyAction().Message}");
        Refresh();
    }

    private void Finish()
    {
        try
        {
            _session.FinishBattle();
            _navigation.NavigateTo(_session.CurrentState);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не вдалося зберегти результат: {ex.Message}. Натисніть «Результат», щоб повторити.";
            Refresh();
        }
    }

    private void Refresh()
    {
        var selectedId = SelectedItem?.Item.Id;
        Items.Clear();
        foreach (var stack in Battle.Player.Inventory.GetItems().Where(s => s.Item is Potion)) Items.Add(stack);
        SelectedItem = Items.FirstOrDefault(s => s.Item.Id == selectedId) ?? Items.FirstOrDefault();
        foreach (var name in new[] { nameof(PlayerHealth), nameof(PlayerMaxHealth), nameof(EnemyHealth), nameof(EnemyMaxHealth), nameof(TurnText) })
            OnPropertyChanged(name);
        CommandManager.InvalidateRequerySuggested();
    }
}
