using System.Collections.ObjectModel;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed record BuildingRow(string Name, int Level, string Status, int Income);

public sealed record BuildOption(BuildingType Type, string Name, int BuildCost, int Income, bool IsAvailable, string Hint);

/// <summary>Лобі: стан міста, будівництво, збереження. Тіки симуляції йдуть із DispatcherTimer.</summary>
public sealed class CityViewModel : ViewModelBase, IDisposable
{
    private readonly GameSession _session;
    private readonly NavigationService _navigation;
    private readonly SaveManager _saves;
    private readonly ICatalogRepository _catalog;
    private readonly DispatcherTimer _timer;
    private string _statusMessage = string.Empty;

    public CityViewModel(NavigationService navigation, SaveManager saves, ICatalogRepository catalog)
    {
        _navigation = navigation;
        _saves = saves;
        _catalog = catalog;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");

        BuildCommand = new RelayCommand(p => Build(p as BuildOption), p => p is BuildOption { IsAvailable: true });
        SaveCommand = new RelayCommand(Save);
        ExitToMenuCommand = new RelayCommand(ExitToMenu);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        Refresh();
    }

    public ObservableCollection<BuildingRow> Buildings { get; } = new();
    public ObservableCollection<BuildOption> BuildOptions { get; } = new();

    public string PlayerName => _session.Player.Name;
    public string CityName => _session.City.Name;
    public int Gold => _session.Player.Gold;
    public int Level => _session.Player.Level;
    public int Population => _session.City.Population;

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand BuildCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ExitToMenuCommand { get; }

    private void OnTick()
    {
        _session.Tick(_timer.Interval);
        RefreshStats();
    }

    private void Build(BuildOption? option)
    {
        if (option is null) return;
        try
        {
            var building = _session.City.BuildBuilding(option.Type);
            StatusMessage = $"Збудовано: {building.Name}.";
        }
        catch (GameRuleException ex)
        {
            StatusMessage = ex.Message;
        }
        Refresh();
    }

    private void Save()
    {
        try
        {
            _session.Save();
            StatusMessage = "Гру збережено.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Помилка збереження: {ex.Message}";
        }
    }

    /// <summary>«Зберегти та вийти в меню»: Місто → Пауза → Головне меню.</summary>
    private void ExitToMenu()
    {
        try
        {
            _session.ChangeState(GameState.Paused);
            _session.Save();
            _session.ChangeState(GameState.MainMenu);
            _navigation.NavigateTo(GameState.MainMenu);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не вдалося вийти в меню: {ex.Message}";
        }
    }

    private void RefreshStats()
    {
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Population));
        OnPropertyChanged(nameof(Level));
    }

    private void Refresh()
    {
        RefreshStats();

        Buildings.Clear();
        foreach (var b in _session.City.GetBuildings())
            Buildings.Add(new BuildingRow(b.Name, b.Level, b.Status.ToString(), b.IncomePerTick));

        BuildOptions.Clear();
        foreach (var type in _catalog.GetBuildingTypes())
        {
            var available = _session.City.GetAvailability(type) == BuildingStatus.Available;
            BuildOptions.Add(new BuildOption(type, type.Name, type.BuildCost, type.IncomePerTick, available,
                available ? "Доступна" : "Потрібна навичка"));
        }
    }

    public void Dispose() => _timer.Stop();
}
