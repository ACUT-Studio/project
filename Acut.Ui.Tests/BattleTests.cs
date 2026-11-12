using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Acut.Data;
using Acut.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Acut.Tests.Core;
using acut.Services;
using acut.ViewModels;
using acut.Views;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class BattleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CityToBattleToResultToCity_UsesCatalogAndSavesResult(bool victory) => RunSta(() => Story(victory, false));

    internal static void VerifyViews()
    {
        Story(true, true);
        Story(false, true);
        PurchasedElixir_AllCatalogEnemiesRenderAndAllowPotionUse();
    }

    private static void PurchasedElixir_AllCatalogEnemiesRenderAndAllowPotionUse()
    {
        var factory = new SqliteConnectionFactory(new SqliteConnectionStringBuilder
        {
            DataSource = $"battle-ui-{Guid.NewGuid():N}", Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared, Pooling = false
        });
        using var anchor = factory.CreateConnection();
        new DatabaseInitializer(factory).Initialize();
        var catalog = new SqliteCatalogRepository(factory);
        foreach (var enemy in catalog.GetEnemies())
        {
            var saves = new SaveManager(new MemoryRepository());
            var session = GameSession.StartNewGame("Герой", null, catalog);
            saves.Register(session);
            session.Player.AddResources(new ResourceAmounts(1000, 1000, 1000));
            session.City.BuildBuilding(catalog.GetBuildingTypes().Single(t => t.Kind == BuildingKind.Market));
            NavigationService? navigation = null;
            navigation = new NavigationService(new TestServices(type => type == typeof(CityViewModel)
                ? new CityViewModel(navigation!, saves, catalog) : new BattleViewModel(navigation!, saves)));
            navigation.NavigateTo<CityViewModel>();
            var city = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
            var elixir = city.MarketItems.Single(i => i is Potion && i.Price == 200);
            var gold = session.Player.Gold;
            city.BuyItemCommand.Execute(elixir);
            Assert.Equal(gold - 200, session.Player.Gold);
            city.StartBattleCommand.Execute(city.Enemies.Single(e => e.Id == enemy.Id));
            var battle = Assert.IsType<BattleViewModel>(navigation.CurrentViewModel);
            var view = LoadView(new BattleView { DataContext = battle });
            var combo = (ComboBox)view.FindName("BattleItems");
            Assert.Equal(elixir.Id, ((ItemStack)combo.SelectedItem).Item.Id);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            view.UpdateLayout();
            var text = string.Join(" ", VisualDescendants(combo).OfType<TextBlock>()
                .Select(label => new TextRange(label.ContentStart, label.ContentEnd).Text));
            Assert.Contains(elixir.Name, text);
            Assert.Contains("1", text);
            session.Player.TakeDamage(50);
            Assert.True(battle.UseItemCommand.CanExecute(null));
            battle.UseItemCommand.Execute(null);
            view.UpdateLayout();
            Assert.Equal(0, session.Player.Inventory.GetQuantity(elixir));
            Assert.Empty(battle.Items);
            Assert.Null(combo.SelectedItem);
            Assert.Equal(2, battle.Log.Count);
        }
    }

    private static void Story(bool victory, bool render)
    {
        var catalog = new BattleCatalog(victory);
        var repository = new MemoryRepository();
        var saves = new SaveManager(repository);
        var session = GameSession.StartNewGame("Герой", null, catalog);
        saves.Register(session);
        NavigationService? navigation = null;
        var services = new TestServices(type => type == typeof(BattleViewModel) ? new BattleViewModel(navigation!, saves)
            : type == typeof(VictoryViewModel) ? new VictoryViewModel(navigation!, saves)
            : type == typeof(CityViewModel) ? new CityViewModel(navigation!, saves, catalog) : null);
        navigation = new NavigationService(services);
        navigation.NavigateTo<CityViewModel>();
        var city = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
        city.OpenBattleCommand.Execute(null);
        if (render)
        {
            var cityView = LoadView(new CityView { DataContext = city });
            Assert.Equal(Visibility.Visible, ((Border)cityView.FindName("EnemySelectionPanel")).Visibility);
            Preview(cityView, "enemy-selection.png");
        }
        Assert.True(city.IsEnemySelectionOpen);
        city.CloseEnemySelectionCommand.Execute(null);
        Assert.False(city.IsEnemySelectionOpen);
        city.OpenBattleCommand.Execute(null);
        Assert.False(city.StartBattleCommand.CanExecute(null));
        city.StartBattleCommand.Execute(city.Enemies.Single());
        var battle = Assert.IsType<BattleViewModel>(navigation.CurrentViewModel);
        Assert.Equal(GameState.Battle, session.CurrentState);
        Assert.False(battle.UseItemCommand.CanExecute(null));
        if (render)
        {
            var view = LoadView(new BattleView { DataContext = battle });
            Assert.Null(view.FindName("HealButton"));
            Assert.False(((Button)view.FindName("UseItemButton")).IsEnabled);
            var appearance = (ContentControl)view.FindName("PlayerAppearance");
            Assert.Equal(session.Player.Appearance, appearance.Content);
            Assert.Equal(session.ActiveBattle!.Enemy.MaxHealth, ((ProgressBar)view.FindName("EnemyHealthBar")).Maximum);
            Preview(view, victory ? "battle.png" : "battle-defeat.png");
        }
        var gold = session.Player.Gold;
        battle.AttackCommand.Execute(null);
        var result = Assert.IsType<VictoryViewModel>(navigation.CurrentViewModel);
        Assert.Equal(victory, result.IsVictory);
        Assert.NotEmpty(battle.Log);
        Assert.False(battle.AttackCommand.CanExecute(null));
        Assert.Equal(victory ? gold + 37 : gold - gold / 10, session.Player.Gold);
        Assert.Equal(victory ? GameState.Victory : GameState.Defeat, repository.State);
        if (render)
        {
            var resultView = LoadView(new VictoryView { DataContext = result });
            Preview(resultView, victory ? "victory.png" : "defeat.png");
        }
        result.BackCommand.Execute(null);
        using var returned = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
        Assert.Equal(GameState.City, repository.State);
        Assert.Null(session.ActiveBattle);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public void ItemCommand_ConsumesInventoryAndRefreshesHealthSelectionAndLog() => RunSta(() =>
    {
        var saves = new SaveManager(new MemoryRepository());
        var session = GameSession.StartNewGame("Герой", null, new FakeCatalog());
        saves.Register(session);
        session.Player.TakeDamage(50);
        session.Player.Inventory.AddItem(FakeCatalog.SmallHealPotion.Clone());
        session.StartBattle(new Enemy("Ворог", EnemyType.Goblin, 100, 10, 3, 0, 0, new AggressiveStrategy()));
        var vm = new BattleViewModel(new NavigationService(new TestServices(_ => null)), saves);
        Assert.True(vm.UseItemCommand.CanExecute(null));
        vm.UseItemCommand.Execute(null);
        Assert.Equal(67, vm.PlayerHealth);
        Assert.Empty(vm.Items);
        Assert.Null(vm.SelectedItem);
        Assert.False(vm.UseItemCommand.CanExecute(null));
        Assert.Equal(2, vm.Log.Count);
        Assert.StartsWith("Хід 1 · Ворог:", vm.Log[0]);
        Assert.StartsWith("Хід 1 · Герой:", vm.Log[1]);
        vm.AttackCommand.Execute(null);
        Assert.Equal(4, vm.Log.Count);
        Assert.StartsWith("Хід 2 · Ворог:", vm.Log[0]);
        Assert.StartsWith("Хід 2 · Герой:", vm.Log[1]);
        Assert.StartsWith("Хід 1 · Ворог:", vm.Log[2]);
        Assert.StartsWith("Хід 1 · Герой:", vm.Log[3]);
    });

    [Fact]
    public void FailedResultSave_CanRetryWithoutDuplicatingRewards() => RunSta(() =>
    {
        var repository = new MemoryRepository { FailSave = true };
        var saves = new SaveManager(repository);
        var session = GameSession.StartNewGame("Герой", null, new FakeCatalog());
        saves.Register(session);
        session.StartBattle(new Enemy("Ворог", EnemyType.Goblin, 1, 0, 0, 37, 20, new AggressiveStrategy()));
        NavigationService? navigation = null;
        navigation = new NavigationService(new TestServices(type => type == typeof(VictoryViewModel)
            ? new VictoryViewModel(navigation!, saves) : new CityViewModel(navigation!, saves, new FakeCatalog())));
        var vm = new BattleViewModel(navigation, saves);
        vm.AttackCommand.Execute(null);
        Assert.NotEmpty(vm.StatusMessage);
        Assert.True(vm.ContinueCommand.CanExecute(null));
        var gold = session.Player.Gold;
        repository.FailSave = false;
        vm.ContinueCommand.Execute(null);
        var result = Assert.IsType<VictoryViewModel>(navigation.CurrentViewModel);
        Assert.Equal(gold, session.Player.Gold);
        repository.FailSave = true;
        var rewardText = result.RewardGold;
        result.BackCommand.Execute(null);
        Assert.Same(result, navigation.CurrentViewModel);
        Assert.NotEmpty(result.StatusMessage);
        Assert.Equal(rewardText, result.RewardGold);
        repository.FailSave = false;
        result.BackCommand.Execute(null);
        Assert.Equal(GameState.City, repository.State);
        using var returned = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
    });

    [Fact]
    public void AutoSave_SavesCityAndBattleButSkipsPauseAndReportsErrors() => RunSta(() =>
    {
        var repository = new MemoryRepository();
        var saves = new SaveManager(repository);
        using var autoSave = new AutoSaveService(saves);
        autoSave.SaveIfPlaying();
        Assert.Equal(0, repository.SaveCount);
        var session = GameSession.StartNewGame("Герой", null, new FakeCatalog());
        saves.Register(session);
        autoSave.SaveIfPlaying();
        session.ChangeState(GameState.Paused);
        autoSave.SaveIfPlaying();
        Assert.Equal(1, repository.SaveCount);
        session.ChangeState(GameState.City);
        session.StartBattle(new Enemy("Ворог", EnemyType.Goblin, 100, 10, 0, 0, 0, new AggressiveStrategy()));
        autoSave.SaveIfPlaying();
        Assert.Equal(GameState.Battle, repository.State);
        repository.FailSave = true;
        autoSave.SaveIfPlaying();
        Assert.NotEmpty(autoSave.StatusMessage);
        repository.FailSave = false;
        autoSave.SaveIfPlaying();
        Assert.Empty(autoSave.StatusMessage);
    });

    private static T LoadView<T>(T view) where T : FrameworkElement
    {
        view.Measure(new Size(1180, 720));
        view.Arrange(new Rect(0, 0, 1180, 720));
        view.UpdateLayout();
        return view;
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in VisualDescendants(child)) yield return descendant;
        }
    }

    private static void Preview(FrameworkElement view, string name)
    {
        var directory = Environment.GetEnvironmentVariable("ACUT_UI_PREVIEW_PATH");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(1180, 720, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(directory, name));
        encoder.Save(file);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class TestServices(Func<Type, object?> resolve) : IServiceProvider
    {
        public object? GetService(Type serviceType) => resolve(serviceType);
    }

    private sealed class MemoryRepository : ISaveRepository
    {
        public int SaveCount { get; private set; }
        public GameState State { get; private set; }
        public bool FailSave { get; set; }
        public void Save(GameSession session)
        {
            if (FailSave) throw new InvalidOperationException("Помилка тестового сховища.");
            SaveCount++;
            State = session.CurrentState;
        }
        public GameSession? Load(Guid saveId) => null;
        public IReadOnlyList<SaveSlotInfo> ListSaves() => [];
        public void Delete(Guid saveId) { }
    }

    private sealed class BattleCatalog(bool victory) : ICatalogRepository
    {
        private readonly FakeCatalog _inner = new();
        public IReadOnlyList<EnemyDefinition> GetEnemies() =>
            [new(101, EnemyType.Goblin, "Тестовий ворог", victory ? 1 : 100, victory ? 1 : 1000, 0, 37, 20, StrategyType.Aggressive)];
        public NewGameEconomy GetNewGameEconomy() => _inner.GetNewGameEconomy();
        public IReadOnlyList<BuildingType> GetBuildingTypes() => _inner.GetBuildingTypes();
        public IReadOnlyList<Item> GetItems() => _inner.GetItems();
        public IReadOnlyList<Recipe> GetRecipes() => _inner.GetRecipes();
        public IReadOnlyList<Skill> GetSkills() => _inner.GetSkills();
        public IReadOnlyList<Quest> GetQuests() => _inner.GetQuests();
        public IReadOnlyList<Achievement> GetAchievements() => _inner.GetAchievements();
        public IReadOnlyList<Stock> GetStocks() => _inner.GetStocks();
    }
}
