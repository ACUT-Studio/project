using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Services;
using Acut.Tests.Core;
using acut.Services;
using acut.ViewModels;
using acut.Views;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class LobbyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345")]
    public void InvalidName_DisablesConfirmationAndExplainsReason(string name) => RunSta(() =>
    {
        var context = CreateContext();
        var vm = new CharacterCreationViewModel(context.Navigation, context.Saves, new FakeCatalog(), new DialogService());
        vm.Name = name;
        Assert.NotEmpty(vm.NameError!);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
        vm.ConfirmCommand.Execute(null);
        Assert.Equal(0, context.Repository.SaveCount);
    });

    [Fact]
    public void ValidName_ClearsErrorAndNotifiesView() => RunSta(() =>
    {
        var context = CreateContext();
        var vm = new CharacterCreationViewModel(context.Navigation, context.Saves, new FakeCatalog(), new DialogService());
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        vm.Name = "  Марко  ";
        Assert.Null(vm.NameError);
        Assert.True(vm.ConfirmCommand.CanExecute(null));
        Assert.Contains(nameof(vm.NameError), notified);
        Assert.Null(GameSession.GetPlayerNameError(new string('а', GameConfig.MaxPlayerNameLength)));
    });

    [Fact]
    public void BuildAndUpgrade_UseDomainCostsAndRefreshIncome() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        vm.BuildCommand.Execute(vm.BuildOptions.Single(o => o.Type.Id == FakeCatalog.Hut.Id));
        var before = context.Session.Player.Gold;
        var row = Assert.Single(vm.Buildings);
        Assert.True(vm.UpgradeCommand.CanExecute(row));
        vm.UpgradeCommand.Execute(row);
        Assert.Equal(before - FakeCatalog.Hut.UpgradeCost, vm.Gold);
        Assert.Equal(2, Assert.Single(vm.Buildings).Level);
        Assert.Equal(FakeCatalog.Hut.IncomePerTick * 2, vm.IncomePerTick);
        vm.UpgradeCommand.Execute(Assert.Single(vm.Buildings));
        Assert.False(vm.UpgradeCommand.CanExecute(Assert.Single(vm.Buildings)));
        Assert.Contains("максимального", Assert.Single(vm.Buildings).Hint);
    });

    [Fact]
    public void LockedBuilding_ShowsRequiredSkillAndCannotBeBuilt() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        var option = vm.BuildOptions.Single(o => o.Type.Id == FakeCatalog.Mine.Id);
        Assert.False(vm.BuildCommand.CanExecute(option));
        Assert.Contains("Основи будівництва", option.Hint);
        vm.BuildCommand.Execute(option);
        Assert.Empty(vm.Buildings);
        Assert.Contains("заблокована", vm.StatusMessage);
    });

    [Fact]
    public void InsufficientGold_ShowsDomainErrorWithoutAddingBuilding() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        context.Session.Player.SpendGold(context.Session.Player.Gold);
        vm.BuildCommand.Execute(vm.BuildOptions.First());
        Assert.Empty(vm.Buildings);
        Assert.Contains("Недостатньо валюти", vm.StatusMessage);
    });

    [Fact]
    public void EscapeCommand_PausesTimerBlocksActionsAndResumesIncome() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        vm.BuildCommand.Execute(vm.BuildOptions.First());
        context.Services.Resolve = type => type == typeof(CityViewModel) ? vm : null;
        context.Navigation.NavigateTo<CityViewModel>();
        var main = new MainViewModel(context.Navigation);
        main.TogglePauseCommand.Execute(null);
        var gold = vm.Gold;
        Assert.True(vm.IsPaused);
        Assert.False(vm.BuildCommand.CanExecute(vm.BuildOptions.First()));
        Assert.False(vm.UpgradeCommand.CanExecute(vm.Buildings.First()));
        vm.BuildCommand.Execute(vm.BuildOptions.First());
        vm.UpgradeCommand.Execute(vm.Buildings.First());
        Pump(TimeSpan.FromMilliseconds(1200));
        Assert.Equal(gold, vm.Gold);
        Assert.Single(vm.Buildings);
        Assert.Equal(1, vm.Buildings[0].Level);
        vm.ResumeCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(1200));
        Assert.False(vm.IsPaused);
        Assert.True(vm.Gold > gold);
    });

    [Fact]
    public void ExitFromPause_SavesBeforeNavigatingToMenu() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        vm.TogglePauseCommand.Execute(null);
        vm.ExitToMenuCommand.Execute(null);
        Assert.Equal(1, context.Repository.SaveCount);
        Assert.Equal(GameState.Paused, context.Repository.SavedState);
        Assert.Equal(GameState.MainMenu, context.Session.CurrentState);
        Assert.IsType<MainMenuViewModel>(context.Navigation.CurrentViewModel);
    });

    [Fact]
    public void FailedSave_StaysPausedAndCanResumeOrRetry() => RunSta(() =>
    {
        var context = CreateContext();
        using var vm = context.City;
        context.Repository.FailSave = true;
        vm.ExitToMenuCommand.Execute(null);
        Assert.True(vm.IsPaused);
        Assert.Null(context.Navigation.CurrentViewModel);
        Assert.Contains("Не вдалося", vm.StatusMessage);
        vm.ResumeCommand.Execute(null);
        Assert.Equal(GameState.City, context.Session.CurrentState);
        context.Repository.FailSave = false;
        vm.ExitToMenuCommand.Execute(null);
        Assert.IsType<MainMenuViewModel>(context.Navigation.CurrentViewModel);
    });

    [Fact]
    public void LoadedPausedSession_OpensPauseOverlayAndResumes() => RunSta(() =>
    {
        var context = CreateContext();
        context.City.Dispose();
        context.Session.ChangeState(GameState.Paused);
        context.Services.Resolve = type => type == typeof(CityViewModel)
            ? new CityViewModel(context.Navigation, context.Saves, new FakeCatalog()) : null;
        context.Navigation.NavigateTo(GameState.Paused);
        using var vm = Assert.IsType<CityViewModel>(context.Navigation.CurrentViewModel);
        Assert.True(vm.IsPaused);
        vm.ResumeCommand.Execute(null);
        Assert.Equal(GameState.City, context.Session.CurrentState);
    });

    [Fact]
    public void Views_LoadWithSharedResources() => RunSta(() =>
    {
        var app = new acut.App();
        app.InitializeComponent();
        try
        {
            var context = CreateContext();
            using var vm = context.City;
            var city = new CityView { DataContext = vm };
            city.Measure(new Size(1180, 720));
            city.Arrange(new Rect(0, 0, 1180, 720));
            vm.TogglePauseCommand.Execute(null);
            city.UpdateLayout();
            var character = new CharacterCreationView
            {
                DataContext = new CharacterCreationViewModel(context.Navigation, context.Saves, new FakeCatalog(), new DialogService())
            };
            character.Measure(new Size(1180, 720));
            character.Arrange(new Rect(0, 0, 1180, 720));
        }
        finally { app.Shutdown(); }
    });

    private static Context CreateContext()
    {
        var repository = new MemorySaveRepository();
        var saves = new SaveManager(repository);
        var session = GameSession.StartNewGame("Марко", null, new FakeCatalog());
        saves.Register(session);
        var services = new TestServices();
        var navigation = new NavigationService(services);
        services.Resolve = type => type == typeof(MainMenuViewModel)
            ? new MainMenuViewModel(navigation, saves, new DialogService()) : null;
        return new Context(repository, saves, session, services, navigation,
            new CityViewModel(navigation, saves, new FakeCatalog()));
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed record Context(MemorySaveRepository Repository, SaveManager Saves,
        GameSession Session, TestServices Services, NavigationService Navigation, CityViewModel City);

    private sealed class TestServices : IServiceProvider
    {
        public Func<Type, object?> Resolve { get; set; } = _ => null;
        public object? GetService(Type serviceType) => Resolve(serviceType);
    }

    private sealed class MemorySaveRepository : ISaveRepository
    {
        public int SaveCount { get; private set; }
        public GameState SavedState { get; private set; }
        public bool FailSave { get; set; }
        public void Save(GameSession session)
        {
            if (FailSave) throw new InvalidOperationException("Помилка тестового сховища.");
            SaveCount++;
            SavedState = session.CurrentState;
        }
        public GameSession? Load(Guid saveId) => null;
        public IReadOnlyList<SaveSlotInfo> ListSaves() => [];
        public void Delete(Guid saveId) { }
    }
}
