using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Services;
using acut.Services;

namespace acut.ViewModels;

public sealed record BuildingRow(Building Building, string Name, int Level, string Status, int Income,
    int UpgradeCost, bool CanUpgrade, string Hint)
{
    public int TypeId => Building.TypeId;
    public int DemolitionRefund => Building.DemolitionRefund;
    public string BuildCostText => ResourceDisplay.Format(Building.BuildResources);
    public string UpgradeCostText => ResourceDisplay.Format(Building.UpgradeResources);
    public string RefundText => ResourceDisplay.Format(Building.DemolitionResources);
    public string IncomeText => ResourceDisplay.Format(Building.OutputPerTick);
}
public sealed record CityPlot(int Index, BuildingRow? Building, bool IsSelected)
{
    public bool IsOccupied => Building is not null;
}
public sealed record BuildOption(BuildingType Type, string Name, int BuildCost, int Income, bool IsAvailable, string Hint)
{
    public string CostText => ResourceDisplay.Format(Type.BuildResources);
    public string IncomeText => ResourceDisplay.Format(BuildingFactory.Create(Type).ProducedResource switch
    {
        ResourceType.Gold => new ResourceAmounts(Type.IncomePerTick),
        ResourceType.Wood => new ResourceAmounts(wood: Type.ProductionRate),
        ResourceType.Stone => new ResourceAmounts(stone: Type.ProductionRate),
        _ => new ResourceAmounts()
    });
}
public sealed record CraftingOption(Recipe Recipe, string Name, string Ingredients, bool CanCraft);
public sealed record StockRow(Stock Stock, string Name, decimal Price, int Quantity);

/// <summary>Лобі та пауза. Таймер працює лише в активному місті.</summary>
public sealed class CityViewModel : ViewModelBase, IDisposable
{
    private readonly GameSession _session;
    private readonly NavigationService _navigation;
    private readonly IReadOnlyList<BuildingType> _buildingTypes;
    private readonly DispatcherTimer _timer;
    private string _statusMessage = string.Empty;
    private bool _disposed;
    private BuildingRow? _selectedBuilding;
    private bool _isConstructionOpen;
    private bool _isCraftingOpen;
    private bool _isMarketOpen;
    private bool _isEnemySelectionOpen;
    private readonly IReadOnlyList<Recipe> _recipes;
    public const int MapColumns = 6;
    private const int MinimumMapRows = 3;
    public const int PlotWidth = 130;
    public const int PlotHeight = 110;

    public CityViewModel(NavigationService navigation, SaveManager saves, ICatalogRepository catalog)
    {
        _navigation = navigation;
        _session = saves.Current ?? throw new InvalidOperationException("Гру не розпочато.");
        _buildingTypes = catalog.GetBuildingTypes();
        _recipes = catalog.GetRecipes();
        Enemies = catalog.GetEnemies();

        BuildCommand = new RelayCommand(p => Build(p as BuildOption),
            p => !_disposed && !IsPaused && _session.City.CanBuildMore && p is BuildOption { IsAvailable: true });
        UpgradeCommand = new RelayCommand(p => Upgrade(p as BuildingRow),
            p => !IsPaused && p is BuildingRow row && _session.City.CanUpgradeBuilding(row.Building, out _));
        RemoveBuildingCommand = new RelayCommand(p => RemoveBuilding(p as BuildingRow),
            p => !_disposed && !IsPaused && p is BuildingRow row && _session.City.GetBuildings().Contains(row.Building));
        TogglePauseCommand = new RelayCommand(TogglePause, () => !_disposed);
        ResumeCommand = new RelayCommand(TogglePause, () => IsPaused && !_disposed);
        SelectBuildingCommand = new RelayCommand(p => SelectBuilding(p as BuildingRow), _ => IsPlaying);
        CloseDetailsCommand = new RelayCommand(() => SelectBuilding(null));
        ToggleConstructionCommand = new RelayCommand(() =>
        {
            IsConstructionOpen = !IsConstructionOpen;
            SelectBuilding(null);
            IsCraftingOpen = false;
            IsMarketOpen = false;
        }, () => IsPlaying);
        CloseConstructionCommand = new RelayCommand(() => IsConstructionOpen = false);
        OpenInventoryCommand = new RelayCommand(() => _navigation.NavigateTo<InventoryViewModel>(), () => IsPlaying);
        OpenCraftingCommand = new RelayCommand(() => OpenMechanic(true), () => IsPlaying && HasWorkshop);
        OpenMarketCommand = new RelayCommand(() => OpenMechanic(false), () => IsPlaying && HasMarket);
        CloseMechanicCommand = new RelayCommand(() => { IsCraftingOpen = false; IsMarketOpen = false; });
        CraftCommand = new RelayCommand(p => Craft(p as CraftingOption),
            p => IsPlaying && HasWorkshop && p is CraftingOption { CanCraft: true });
        BuyItemCommand = new RelayCommand(p => BuyItem(p as Item), _ => IsPlaying && HasMarket);
        BuyStockCommand = new RelayCommand(p => TradeStock(p as StockRow, true), _ => IsPlaying && HasMarket);
        SellStockCommand = new RelayCommand(p => TradeStock(p as StockRow, false),
            p => IsPlaying && HasMarket && p is StockRow { Quantity: > 0 });
        OpenBattleCommand = new RelayCommand(() =>
        {
            SelectBuilding(null);
            IsConstructionOpen = false;
            IsCraftingOpen = false;
            IsMarketOpen = false;
            IsEnemySelectionOpen = true;
        }, () => IsPlaying && !_disposed);
        CloseEnemySelectionCommand = new RelayCommand(() => IsEnemySelectionOpen = false);
        StartBattleCommand = new RelayCommand(p => StartBattle(p as EnemyDefinition),
            p => IsPlaying && !_disposed && p is EnemyDefinition enemy && Enemies.Contains(enemy));
        SaveCommand = new RelayCommand(Save);
        ExitToMenuCommand = new RelayCommand(ExitToMenu);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        if (!IsPaused) _timer.Start();
        Refresh();
    }

    public ObservableCollection<BuildingRow> Buildings { get; } = new();
    public ObservableCollection<BuildOption> BuildOptions { get; } = new();
    public ObservableCollection<CityPlot> Plots { get; } = new();
    public ObservableCollection<CraftingOption> CraftingOptions { get; } = new();
    public ObservableCollection<StockRow> Stocks { get; } = new();
    public IReadOnlyList<EnemyDefinition> Enemies { get; }
    public bool IsEnemySelectionOpen { get => _isEnemySelectionOpen; private set => SetProperty(ref _isEnemySelectionOpen, value); }
    public IReadOnlyList<Item> MarketItems => _session.City.Shop?.Assortment ?? [];
    public bool HasWorkshop => _session.City.HasWorkshop;
    public bool HasMarket => _session.City.HasMarket;
    public string CraftingHint => HasWorkshop ? "Зіллєваріння" : "Побудуйте майстерню зілля.";
    public string MarketHint => HasMarket ? "Ринок" : "Побудуйте ринок.";
    public bool IsCraftingOpen { get => _isCraftingOpen; private set => SetProperty(ref _isCraftingOpen, value); }
    public bool IsMarketOpen { get => _isMarketOpen; private set => SetProperty(ref _isMarketOpen, value); }
    public BuildingRow? SelectedBuilding
    {
        get => _selectedBuilding;
        private set
        {
            if (SetProperty(ref _selectedBuilding, value))
                OnPropertyChanged(nameof(IsBuildingSelected));
        }
    }
    public bool IsBuildingSelected => SelectedBuilding is not null;
    public bool IsConstructionOpen
    {
        get => _isConstructionOpen;
        private set => SetProperty(ref _isConstructionOpen, value);
    }
    public string PlayerName => _session.Player.Name;
    public AppearanceSettings Appearance => _session.Player.Appearance;
    public string CityName => _session.City.Name;
    public int BuildingCount => Buildings.Count;
    public int BuildingLimit => GameConfig.MaxCityBuildings;
    public string BuildingCountText => $"Будівлі: {BuildingCount} / {BuildingLimit}";
    public int Gold => _session.Player.Gold;
    public int Wood => _session.Player.Wood;
    public int Stone => _session.Player.Stone;
    public int WoodPerTick => _session.City.GetBuildings().Sum(b => b.OutputPerTick.Wood);
    public int StonePerTick => _session.City.GetBuildings().Sum(b => b.OutputPerTick.Stone);
    public int Level => _session.Player.Level;
    public int Experience => _session.Player.Experience;
    public int Population => _session.City.Population;
    public int IncomePerTick => _session.City.GetBuildings().Sum(b => b.OutputPerTick.Gold);
    public bool IsPaused => _session.CurrentState == GameState.Paused;
    public bool IsPlaying => !IsPaused;
    public bool IsCityEmpty => Buildings.Count == 0;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public RelayCommand SelectBuildingCommand { get; }
    public RelayCommand CloseDetailsCommand { get; }
    public RelayCommand ToggleConstructionCommand { get; }
    public RelayCommand CloseConstructionCommand { get; }
    public RelayCommand OpenInventoryCommand { get; }
    public RelayCommand OpenCraftingCommand { get; }
    public RelayCommand OpenMarketCommand { get; }
    public RelayCommand CloseMechanicCommand { get; }
    public RelayCommand CraftCommand { get; }
    public RelayCommand BuyItemCommand { get; }
    public RelayCommand BuyStockCommand { get; }
    public RelayCommand SellStockCommand { get; }
    public RelayCommand OpenBattleCommand { get; }
    public RelayCommand CloseEnemySelectionCommand { get; }
    public RelayCommand StartBattleCommand { get; }
    public RelayCommand BuildCommand { get; }
    public RelayCommand UpgradeCommand { get; }
    public RelayCommand RemoveBuildingCommand { get; }
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

    private void StartBattle(EnemyDefinition? enemy)
    {
        if (!StartBattleCommand.CanExecute(enemy)) return;
        _session.StartBattle(enemy!.CreateEnemy());
        _navigation.NavigateTo(GameState.Battle);
    }

    private void SelectBuilding(BuildingRow? row)
    {
        if (_disposed) return;
        SelectedBuilding = row is null ? null : Buildings.FirstOrDefault(b => b.Building.Id == row.Building.Id);
        if (SelectedBuilding is not null)
        {
            IsConstructionOpen = false;
            IsCraftingOpen = false;
            IsMarketOpen = false;
        }
        RefreshPlots();
    }

    private void RefreshPlots()
    {
        var count = Math.Max(MapColumns * MinimumMapRows,
            ((Buildings.Count + MapColumns - 1) / MapColumns) * MapColumns);
        for (var i = 0; i < count; i++)
        {
            var row = i < Buildings.Count ? Buildings[i] : null;
            var plot = new CityPlot(i, row, row is not null && row.Building.Id == SelectedBuilding?.Building.Id);
            if (i == Plots.Count) Plots.Add(plot);
            else if (Plots[i] != plot) Plots[i] = plot;
        }
        while (Plots.Count > count) Plots.RemoveAt(Plots.Count - 1);
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

    private void RemoveBuilding(BuildingRow? row)
    {
        if (row is null || IsPaused || _disposed) return;
        try
        {
            var refund = _session.City.RemoveBuilding(row.Building);
            StatusMessage = $"Продано: {row.Name}. Повернуто: {ResourceDisplay.Format(refund)}.";
        }
        catch (GameRuleException ex) { StatusMessage = ex.Message; }
        Refresh();
    }

    private void OpenMechanic(bool crafting)
    {
        if (IsPaused || (crafting ? !HasWorkshop : !HasMarket)) return;
        SelectBuilding(null);
        IsConstructionOpen = false;
        IsCraftingOpen = crafting;
        IsMarketOpen = !crafting;
        RefreshMechanics();
    }

    private void Craft(CraftingOption? option)
    {
        if (option is null || IsPaused || !HasWorkshop) return;
        StatusMessage = _session.City.Craft(option.Recipe) is { } result
            ? $"Виготовлено: {result.Name}." : "Не вистачає інгредієнтів або місця в інвентарі.";
        Refresh();
    }

    private void BuyItem(Item? item)
    {
        if (item is null || IsPaused || !HasMarket) return;
        StatusMessage = _session.City.Shop?.BuyItem(item, _session.Player) == true
            ? $"Придбано: {item.Name}." : "Не вистачає золота або місця в інвентарі.";
        Refresh();
    }

    private void TradeStock(StockRow? row, bool buy)
    {
        if (row is null || IsPaused || !HasMarket) return;
        var success = buy ? _session.City.Market.Buy(row.Stock, 1, _session.Player)
            : _session.City.Market.Sell(row.Stock, 1, _session.Player);
        StatusMessage = success ? "Операцію з акціями виконано." : "Недостатньо золота або акцій.";
        Refresh();
    }

    private void RefreshMechanics()
    {
        if (!HasWorkshop) IsCraftingOpen = false;
        if (!HasMarket) IsMarketOpen = false;
        CraftingOptions.Clear();
        foreach (var recipe in _recipes)
            CraftingOptions.Add(new CraftingOption(recipe, recipe.Name,
                string.Join(", ", recipe.Ingredients.Select(i =>
                    $"{i.Key.Name}: {_session.Player.Inventory.GetQuantity(i.Key)}/{i.Value}")),
                HasWorkshop && recipe.CanCraft(_session.Player.Inventory)));
        Stocks.Clear();
        foreach (var stock in _session.City.Market.Stocks)
            Stocks.Add(new StockRow(stock, stock.Name, stock.Price, _session.Player.StockHoldings.GetValueOrDefault(stock.Id)));
        OnPropertyChanged(nameof(HasWorkshop));
        OnPropertyChanged(nameof(HasMarket));
        OnPropertyChanged(nameof(CraftingHint));
        OnPropertyChanged(nameof(MarketHint));
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
        OnPropertyChanged(nameof(Wood));
        OnPropertyChanged(nameof(Stone));
        OnPropertyChanged(nameof(WoodPerTick));
        OnPropertyChanged(nameof(StonePerTick));
        OnPropertyChanged(nameof(Population));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Experience));
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
        OnPropertyChanged(nameof(BuildingCount));
        OnPropertyChanged(nameof(BuildingCountText));
        if (SelectedBuilding is not null)
            SelectedBuilding = Buildings.FirstOrDefault(b => b.Building.Id == SelectedBuilding.Building.Id);
        RefreshPlots();

        index = 0;
        foreach (var type in _buildingTypes)
        {
            var unlocked = _session.City.GetAvailability(type) == BuildingStatus.Available;
            var failure = _session.City.GetBuildFailureReason(type);
            var available = failure is null;
            var skill = _session.Player.SkillTree.Skills.FirstOrDefault(s => s.Id == type.RequiredSkillId);
            var hint = !unlocked ? $"Потрібна навичка: {skill?.Name ?? type.RequiredSkillId?.ToString()}" : failure ?? "Доступна";
            var option = new BuildOption(type, type.Name, type.BuildCost, type.IncomePerTick, available, hint);
            if (index == BuildOptions.Count) BuildOptions.Add(option);
            else if (BuildOptions[index] != option) BuildOptions[index] = option;
            index++;
        }
        RefreshMechanics();
        CommandManager.InvalidateRequerySuggested();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
