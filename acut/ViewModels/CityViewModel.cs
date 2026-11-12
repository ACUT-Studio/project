using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed record BuildingRow(Building Building, string Name, int Level, string Status, int Income,
    int UpgradeCost, bool CanUpgrade, string Hint);
public sealed record BuildOption(BuildingType Type, string Name, int BuildCost, int Income, bool IsAvailable, string Hint);

/// <summary>Лобі та пауза. Таймер працює лише в активному місті.</summary>
public sealed class CityViewModel : ViewModelBase, IDisposable
{
    private readonly GameSession _session;
    private readonly NavigationService _navigation;
    private readonly IReadOnlyList<BuildingType> _buildingTypes;
    private readonly DispatcherTimer _timer;
    private string _statusMessage = string.Empty;
    private bool _disposed;

    public CityViewModel(NavigationService navigation, SaveManager saves, ICatalogRepository catalog)
    {
        _navigation = navigation;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");
        _buildingTypes = catalog.GetBuildingTypes();

        BuildCommand = new RelayCommand(p => Build(p as BuildOption),
            p => !IsPaused && p is BuildOption { IsAvailable: true });
        UpgradeCommand = new RelayCommand(p => Upgrade(p as BuildingRow),
            p => !IsPaused && p is BuildingRow row && _session.City.CanUpgradeBuilding(row.Building, out _));
        TogglePauseCommand = new RelayCommand(TogglePause, () => !_disposed);
        ResumeCommand = new RelayCommand(TogglePause, () => IsPaused && !_disposed);
        SaveCommand = new RelayCommand(Save);
        ExitToMenuCommand = new RelayCommand(ExitToMenu);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        if (!IsPaused) _timer.Start();
        Refresh();
    }

    public ObservableCollection<BuildingRow> Buildings { get; } = new();
    public ObservableCollection<BuildOption> BuildOptions { get; } = new();
    public string PlayerName => _session.Player.Name;
    public string CityName => _session.City.Name;
    public int Gold => _session.Player.Gold;
    public int Level => _session.Player.Level;
    public int Population => _session.City.Population;
    public int IncomePerTick => _session.City.GetBuildings().Sum(b => b.CollectIncome());
    public bool IsPaused => _session.CurrentState == GameState.Paused;
    public bool IsPlaying => !IsPaused;
    public bool IsCityEmpty => Buildings.Count == 0;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand BuildCommand { get; }
    public RelayCommand UpgradeCommand { get; }
    public RelayCommand TogglePauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ExitToMenuCommand { get; }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed || IsPaused) return;
        _session.Tick(_timer.Interval);
        Refresh();
    }

    private void TogglePause()
    {
        if (_disposed) return;
        if (IsPaused)
        {
            _session.ChangeState(GameState.City);
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _session.ChangeState(GameState.Paused);
        }
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsPlaying));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Build(BuildOption? option)
    {
        if (option is null || IsPaused || _disposed) return;
        try
        {
            var building = _session.City.BuildBuilding(option.Type);
            StatusMessage = $"Збудовано: {building.Name}.";
        }
        catch (GameRuleException ex) { StatusMessage = ex.Message; }
        Refresh();
    }

    private void Upgrade(BuildingRow? row)
    {
        if (row is null || IsPaused || _disposed) return;
        StatusMessage = _session.City.TryUpgradeBuilding(row.Building, out var reason)
            ? $"Покращено: {row.Name}, рівень {row.Building.Level}."
            : reason ?? "Не вдалося покращити будівлю.";
        Refresh();
    }

    private void Save()
    {
        try
        {
            _session.Save();
            StatusMessage = "Гру збережено.";
        }
        catch (Exception ex) { StatusMessage = $"Помилка збереження: {ex.Message}"; }
    }

    private void ExitToMenu()
    {
        if (!IsPaused) TogglePause();
        try
        {
            _session.Save();
            _session.ChangeState(GameState.MainMenu);
            _navigation.NavigateTo(GameState.MainMenu);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не вдалося вийти в меню: {ex.Message}";
        }
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Population));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IncomePerTick));

        var index = 0;
        foreach (var b in _session.City.GetBuildings())
        {
            var canUpgrade = _session.City.CanUpgradeBuilding(b, out var reason);
            var row = new BuildingRow(b, b.Name, b.Level,
                b.Status == BuildingStatus.MaxLevel ? "Максимальний рівень" : "Активна",
                b.CollectIncome(), b.UpgradeCost, canUpgrade, reason ?? "Доступне покращення");
            if (index == Buildings.Count) Buildings.Add(row);
            else if (Buildings[index] != row) Buildings[index] = row;
            index++;
        }
        while (Buildings.Count > index) Buildings.RemoveAt(Buildings.Count - 1);
        OnPropertyChanged(nameof(IsCityEmpty));

        index = 0;
        foreach (var type in _buildingTypes)
        {
            var available = _session.City.GetAvailability(type) == BuildingStatus.Available;
            var skill = _session.Player.SkillTree.Skills.FirstOrDefault(s => s.Id == type.RequiredSkillId);
            var hint = available ? "Доступна"
                : $"Потрібна навичка: {skill?.Name ?? type.RequiredSkillId?.ToString()}";
            var option = new BuildOption(type, type.Name, type.BuildCost, type.IncomePerTick, available, hint);
            if (index == BuildOptions.Count) BuildOptions.Add(option);
            else if (BuildOptions[index] != option) BuildOptions[index] = option;
            index++;
        }
        CommandManager.InvalidateRequerySuggested();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
