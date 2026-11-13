using System.Runtime.ExceptionServices;
using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using acut.Services;
using acut.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Acut.Ui.Tests.TestHelpers;

internal sealed class UiTestContext : IDisposable
{
    public UiTestContext(ResourceAmounts? startingResources = null)
    {
        Catalog = new UiFakeCatalog(startingResources ?? new ResourceAmounts(1000, 1000, 1000));
        SaveRepository = new UiMemorySaveRepository();
        Saves = new SaveManager(SaveRepository);

        var services = new ServiceCollection();
        services.AddSingleton<ICatalogRepository>(Catalog);
        services.AddSingleton<ISaveRepository>(SaveRepository);
        services.AddSingleton(Saves);
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<AutoSaveService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<CharacterCreationViewModel>();
        services.AddTransient<CityViewModel>();
        services.AddTransient<LoadingViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<CraftingViewModel>();
        services.AddTransient<BattleViewModel>();
        services.AddTransient<VictoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();
        Navigation = Services.GetRequiredService<NavigationService>();
    }

    public UiFakeCatalog Catalog { get; }
    public UiMemorySaveRepository SaveRepository { get; }
    public SaveManager Saves { get; }
    public ServiceProvider Services { get; }
    public NavigationService Navigation { get; }

    public GameSession StartAndRegisterSession(string playerName = "Герой")
    {
        var session = GameSession.StartNewGame(playerName, AppearanceSettings.Default, Catalog);
        Saves.Register(session);
        return session;
    }

    public void Dispose() => Services.Dispose();
}

internal static class StaTestRunner
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

internal sealed class UiMemorySaveRepository : ISaveRepository
{
    private readonly Dictionary<Guid, GameSession> _storage = new();

    public bool ThrowOnSave { get; set; }
    public int SaveCalls { get; private set; }

    public void Save(GameSession session)
    {
        if (ThrowOnSave)
            throw new InvalidOperationException("Помилка збереження у тестовому сховищі.");
        SaveCalls++;
        _storage[session.SessionId] = session;
    }

    public GameSession? Load(Guid saveId) => _storage.GetValueOrDefault(saveId);

    public IReadOnlyList<SaveSlotInfo> ListSaves() =>
        _storage.Values
            .Select(s => new SaveSlotInfo(s.SessionId, s.Player.Name, DateTime.UtcNow))
            .ToList();

    public void Delete(Guid saveId) => _storage.Remove(saveId);
}

internal sealed class UiFakeCatalog : ICatalogRepository
{
    private readonly ResourceAmounts _startingResources;

    public UiFakeCatalog(ResourceAmounts startingResources)
    {
        _startingResources = startingResources;
    }

    public static readonly BuildingType Hut = new(1, BuildingKind.Residential, "Хатина", 0, 0, 3, 2, 4, "Gold", 0, null, 100, 0, 80, 0);
    public static readonly BuildingType Sawmill = new(3, BuildingKind.Production, "Лісопил", 0, 0, 3, 0, 0, "Wood", 4, null, 120, 0, 100, 0);
    public static readonly BuildingType Mine = new(4, BuildingKind.Production, "Шахта", 0, 0, 3, 0, 0, "Stone", 4, 1, 150, 0, 120, 0);
    public static readonly BuildingType Workshop = new(5, BuildingKind.Workshop, "Майстерня зілля", 100, 0, 1, 0, 0, null, 0, null, 120, 100, 0, 0);
    public static readonly BuildingType Market = new(6, BuildingKind.Market, "Ринок", 100, 0, 1, 0, 0, null, 0, null, 150, 150, 0, 0);

    public static readonly Potion SmallPotion = new(1, "Мале зілля лікування", "Відновлює 25 HP", 30, PotionEffect.Heal, 25);
    public static readonly Potion LargePotion = new(2, "Велике зілля лікування", "Відновлює 60 HP", 80, PotionEffect.Heal, 60);
    public static readonly Weapon IronSword = new(4, "Залізний меч", "+5 до атаки", 150, 5);
    public static readonly Armor LeatherArmor = new(6, "Шкіряна броня", "+3 до захисту", 120, 3);

    public static readonly Recipe LargePotionRecipe = new(
        1,
        "Велике зілля лікування",
        LargePotion,
        new Dictionary<Item, int> { [SmallPotion] = 2 });

    public static readonly EnemyDefinition Goblin = new(1, EnemyType.Goblin, "Гоблін", 20, 5, 1, 25, 20, StrategyType.Aggressive);

    public NewGameEconomy GetNewGameEconomy() => new(_startingResources, StartingBuildingTypeId: 3);
    public IReadOnlyList<BuildingType> GetBuildingTypes() => [Hut, Sawmill, Mine, Workshop, Market];
    public IReadOnlyList<Item> GetItems() => [SmallPotion.Clone(), LargePotion.Clone(), IronSword.Clone(), LeatherArmor.Clone()];
    public IReadOnlyList<Recipe> GetRecipes() => [LargePotionRecipe];
    public IReadOnlyList<Skill> GetSkills() => [new Skill(1, "Основи будівництва", "", 1)];
    public IReadOnlyList<Quest> GetQuests() => [new Quest(1, "Перша будівля", "", GameEventType.BuildingBuilt, 1, 50, 20)];
    public IReadOnlyList<Achievement> GetAchievements() => [new Achievement(1, "Перша перемога", "", GameEventType.BattleWon, 1)];
    public IReadOnlyList<EnemyDefinition> GetEnemies() => [Goblin];
    public IReadOnlyList<Stock> GetStocks() => [new Stock(1, "AGRO", "Агро-Холдинг", 100m, 0.05m)];
}