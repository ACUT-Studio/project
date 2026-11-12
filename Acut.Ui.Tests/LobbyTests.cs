using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Acut.Core.Domain.Economy;
using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Acut.Tests.Core;
using acut.Services;
using acut.ViewModels;
using acut.Views;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class LobbyTests
{
    [Fact]
    public void Progress_LearningImmediatelyEnablesRequiredBuildingPurchase() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        var locked = city.BuildOptions.Single(o => o.Type.Id == FakeCatalog.Mine.Id);
        Assert.False(locked.IsAvailable);
        Assert.Contains("Основи будівництва", locked.Hint);
        Assert.False(city.BuildCommand.CanExecute(locked));
        var resources = context.Session.Player.Resources;
        city.BuildCommand.Execute(locked);
        Assert.Empty(city.Buildings);
        Assert.Equal(resources, context.Session.Player.Resources);
        city.Progress.LearnCommand.Execute(Assert.Single(city.Progress.Skills));
        var available = city.BuildOptions.Single(o => o.Type.Id == FakeCatalog.Mine.Id);
        Assert.True(available.IsAvailable);
        Assert.True(city.BuildCommand.CanExecute(available));
        city.BuildCommand.Execute(available);
        Assert.Equal(FakeCatalog.Mine.Id, Assert.Single(city.Buildings).TypeId);
    });

    [Fact]
    public void Progress_LearningUsesPointsAndPrerequisites() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        var root = new Skill(10, "Основи", "", 1);
        var child = new Skill(11, "Розвиток", "", 2);
        child.Prerequisites.Add(root);
        var tree = context.Session.Player.SkillTree;
        tree.Initialize([root, child]);
        tree.SkillPoints = 3;
        city.OpenProgressCommand.Execute(null);
        var progress = city.Progress;
        Assert.True(city.IsProgressOpen);
        Assert.Equal("Доступна", progress.Skills[0].State);
        Assert.Equal("Заблокована", progress.Skills[1].State);
        Assert.Contains("Основи", progress.Skills[1].Prerequisites);
        progress.LearnCommand.Execute(progress.Skills[1]);
        Assert.Equal(3, tree.SkillPoints);
        progress.LearnCommand.Execute(progress.Skills[0]);
        Assert.True(root.IsUnlocked);
        Assert.Equal(2, progress.SkillPoints);
        Assert.Equal("Вивчена", progress.Skills[0].State);
        Assert.False(progress.LearnCommand.CanExecute(progress.Skills[0]));
        Assert.Equal("Доступна", progress.Skills[1].State);
        progress.LearnCommand.Execute(progress.Skills[1]);
        Assert.True(child.IsUnlocked);
        Assert.Equal(0, progress.SkillPoints);
        city.CloseProgressCommand.Execute(null);
        Assert.False(city.IsProgressOpen);
    });

    [Fact]
    public void Progress_QuestRewardsRefreshCityAndCannotBeClaimedTwice() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        var quest = new Quest(50, "Будівничий", "Збудуйте хатину", GameEventType.BuildingBuilt, 1, 100, 10);
        context.Session.Player.QuestLog.AddQuest(quest);
        city.OpenProgressCommand.Execute(null);
        var progress = city.Progress;
        var row = progress.Quests.Single(q => q.Quest == quest);
        Assert.False(progress.ClaimRewardCommand.CanExecute(row));
        progress.ClaimRewardCommand.Execute(row);
        city.BuildCommand.Execute(city.BuildOptions.Single(o => o.Type.Id == FakeCatalog.Hut.Id));
        row = progress.Quests.Single(q => q.Quest == quest);
        Assert.Equal(1, row.Progress);
        Assert.True(progress.ClaimRewardCommand.CanExecute(row));
        var gold = city.Gold;
        var experience = city.Experience;
        progress.ClaimRewardCommand.Execute(row);
        Assert.Equal(gold + quest.RewardGold, city.Gold);
        Assert.Equal(experience + quest.RewardExperience, city.Experience);
        Assert.Equal("Нагороду отримано", progress.Quests.Single(q => q.Quest == quest).State);
        progress.ClaimRewardCommand.Execute(row);
        Assert.Equal(gold + quest.RewardGold, city.Gold);
        Assert.False(progress.ClaimRewardCommand.CanExecute(row));
    });

    [Fact]
    public void Progress_CityTimerRefreshesQuestsAndAchievementDatesWhileOpen() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        var quest = new Quest(50, "Прибуток", "Отримайте золото", GameEventType.GoldEarned, 1, 0, 0);
        var achievement = new Achievement(50, "Перший прибуток", "Отримайте золото", GameEventType.GoldEarned, 1);
        context.Session.Player.QuestLog.AddQuest(quest);
        context.Session.Player.Achievements.Initialize([achievement]);
        context.Session.City.BuildBuilding(FakeCatalog.Hut);
        city.OpenProgressCommand.Execute(null);
        Assert.Equal("Заблоковано", Assert.Single(city.Progress.Achievements).State);
        Pump(TimeSpan.FromMilliseconds(1150));
        Assert.Equal(GameState.City, context.Session.CurrentState);
        Assert.Equal(1, city.Progress.Quests.Single(q => q.Quest == quest).Progress);
        var row = Assert.Single(city.Progress.Achievements);
        Assert.Equal("Здобуто", row.State);
        Assert.Equal(achievement.UnlockedAt!.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), row.Date);
        city.TogglePauseCommand.Execute(null);
        Assert.False(city.Progress.ClaimRewardCommand.CanExecute(city.Progress.Quests.Single(q => q.Quest == quest)));
    });

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
        Assert.Contains("Недостатньо ресурсів", vm.StatusMessage);
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
    public void SaveExitAndContinue_ResumesCityWithoutPause() => RunSta(() =>
    {
        var context = CreateContext();
        using var original = context.City;
        original.BuildCommand.Execute(original.BuildOptions.First());
        original.TogglePauseCommand.Execute(null);
        original.ExitToMenuCommand.Execute(null);

        CityViewModel? resumed = null;
        context.Services.Resolve = type => type == typeof(CityViewModel)
            ? resumed = new CityViewModel(context.Navigation, context.Saves, new FakeCatalog()) : null;
        var menu = Assert.IsType<MainMenuViewModel>(context.Navigation.CurrentViewModel);
        Assert.True(menu.ContinueCommand.CanExecute(null));
        menu.ContinueCommand.Execute(null);

        using var city = Assert.IsType<CityViewModel>(context.Navigation.CurrentViewModel);
        Assert.Same(resumed, city);
        Assert.Equal(GameState.City, context.Saves.Current!.CurrentState);
        Assert.False(city.IsPaused);
        Assert.Single(city.Buildings);
        Assert.False(city.IsBuildingSelected);
        var gold = city.Gold;
        Pump(TimeSpan.FromMilliseconds(1200));
        Assert.True(city.Gold > gold);
    });

    [Fact]
    public void BuildingDetails_OpenOnlyAfterSelectionAndRefreshAfterUpgrade() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        Assert.Null(city.SelectedBuilding);
        city.BuildCommand.Execute(city.BuildOptions.First());
        Assert.False(city.IsBuildingSelected);
        city.SelectBuildingCommand.Execute(city.Buildings.First());
        Assert.Equal(city.Buildings.First(), city.SelectedBuilding);
        Assert.Single(city.Plots, p => p.IsSelected);

        var id = city.SelectedBuilding!.Building.Id;
        city.UpgradeCommand.Execute(city.SelectedBuilding);
        Assert.Equal(id, city.SelectedBuilding!.Building.Id);
        Assert.Equal(2, city.SelectedBuilding.Level);
        Assert.Equal(0, city.Plots.Single(p => p.IsSelected).Index);

        city.CloseDetailsCommand.Execute(null);
        Assert.Null(city.SelectedBuilding);
        Assert.False(city.IsBuildingSelected);
        Assert.DoesNotContain(city.Plots, p => p.IsSelected);
    });

    [Fact]
    public void Map_GrowsInCompleteRowsAndPreservesBuildingOrder() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        Assert.Equal(18, city.Plots.Count);
        context.Session.Player.AddGold(10000);
        for (var i = 0; i < 19; i++)
            city.BuildCommand.Execute(city.BuildOptions.First());

        Assert.Equal(24, city.Plots.Count);
        Assert.Equal(19, city.Plots.Count(p => p.IsOccupied));
        Assert.Equal(city.Buildings.Select(b => b.Building.Id),
            city.Plots.Where(p => p.IsOccupied).Select(p => p.Building!.Building.Id));
    });

    [Fact]
    public void ConstructionPanel_StaysOpenAfterPurchasesAndClosesOnSelection() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        Assert.False(city.IsConstructionOpen);
        city.ToggleConstructionCommand.Execute(null);
        Assert.True(city.IsConstructionOpen);
        city.BuildCommand.Execute(city.BuildOptions.First());
        Assert.True(city.IsConstructionOpen);
        Assert.False(city.IsBuildingSelected);
        city.BuildCommand.Execute(city.BuildOptions.First());
        Assert.True(city.IsConstructionOpen);
        Assert.Equal(2, city.Buildings.Count);
        city.SelectBuildingCommand.Execute(city.Buildings.First());
        Assert.False(city.IsConstructionOpen);
        Assert.True(city.IsBuildingSelected);
        city.ToggleConstructionCommand.Execute(null);
        Assert.True(city.IsConstructionOpen);
        Assert.False(city.IsBuildingSelected);
    });
    [Fact]
    public void BuildingLimit_DisablesConstructionAndDemolitionFreesSpace() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        context.Session.Player.AddGold(10000);
        for (var i = 0; i < 50; i++) city.BuildCommand.Execute(city.BuildOptions.First());
        Assert.Equal(50, city.BuildingCount);
        Assert.False(city.BuildCommand.CanExecute(city.BuildOptions.First()));
        Assert.Contains("50", city.BuildOptions.First().Hint);
        city.BuildCommand.Execute(city.BuildOptions.First());
        Assert.Equal(50, city.Buildings.Count);
        city.SelectBuildingCommand.Execute(city.Buildings.First());
        city.UpgradeCommand.Execute(city.SelectedBuilding);
        var row = city.SelectedBuilding!;
        Assert.Equal(90, row.DemolitionRefund);
        var gold = city.Gold;
        city.TogglePauseCommand.Execute(null);
        Assert.False(city.RemoveBuildingCommand.CanExecute(row));
        city.RemoveBuildingCommand.Execute(row);
        Assert.Equal(50, city.BuildingCount);
        Assert.Equal(gold, city.Gold);
        city.ResumeCommand.Execute(null);
        city.RemoveBuildingCommand.Execute(row);
        Assert.Equal(49, city.BuildingCount);
        Assert.Equal(gold + row.DemolitionRefund, city.Gold);
        Assert.Null(city.SelectedBuilding);
        Assert.False(city.IsBuildingSelected);
        Assert.True(city.BuildCommand.CanExecute(city.BuildOptions.First()));
        Assert.DoesNotContain(city.Plots, p => p.Building?.Building.Id == row.Building.Id);
    });
    [Fact]
    public void ResourceUi_StartsWithSawmillAndGatesWorkingCraftingAndMarketPanels() => RunSta(() =>
    {
        var context = CreateContext(new ResourceCatalog());
        using var city = context.City;
        Assert.Equal(0, city.Gold);
        Assert.Equal(150, city.Wood);
        Assert.Equal(0, city.Stone);
        Assert.Equal(4, city.WoodPerTick);
        Assert.Equal(0, city.IncomePerTick);
        Assert.False(city.OpenCraftingCommand.CanExecute(null));
        Assert.False(city.OpenMarketCommand.CanExecute(null));
        city.OpenMarketCommand.Execute(null);
        Assert.False(city.IsMarketOpen);
        city.BuildCommand.Execute(city.BuildOptions.Single(o => o.Type.Id == 4));
        Assert.Equal(0, city.Wood);
        Assert.Equal(4, city.StonePerTick);
        context.Session.Player.AddResources(new(5000, 5000, 5000));
        city.BuildCommand.Execute(city.BuildOptions.Single(o => o.Type.Id == 5));
        city.BuildCommand.Execute(city.BuildOptions.Single(o => o.Type.Id == 6));
        Assert.True(city.OpenCraftingCommand.CanExecute(null));
        Assert.True(city.OpenMarketCommand.CanExecute(null));
        Assert.False(city.BuildCommand.CanExecute(city.BuildOptions.Single(o => o.Type.Id == 6)));
        Assert.Contains("вже є", city.BuildOptions.Single(o => o.Type.Id == 6).Hint);

        city.OpenMarketCommand.Execute(null);
        Assert.True(city.IsMarketOpen);
        var potion = city.MarketItems.Single(i => i.Id == FakeCatalog.SmallHealPotion.Id);
        city.BuyItemCommand.Execute(potion);
        city.BuyItemCommand.Execute(potion);
        Assert.Equal(2, context.Session.Player.Inventory.GetQuantity(potion));
        city.OpenCraftingCommand.Execute(null);
        Assert.True(city.IsCraftingOpen);
        Assert.False(city.IsMarketOpen);
        var recipe = Assert.Single(city.CraftingOptions);
        Assert.True(city.CraftCommand.CanExecute(recipe));
        city.CraftCommand.Execute(recipe);
        Assert.Equal(1, context.Session.Player.Inventory.GetQuantity(FakeCatalog.LargeHealPotion));
        Assert.Equal(1, context.Session.Player.Statistics.PotionsCrafted);
        city.SelectBuildingCommand.Execute(city.Buildings.Single(b => b.Building.Kind == BuildingKind.Workshop));
        city.RemoveBuildingCommand.Execute(city.SelectedBuilding);
        Assert.False(city.HasWorkshop);
        Assert.False(city.OpenCraftingCommand.CanExecute(null));
        city.SelectBuildingCommand.Execute(city.Buildings.Single(b => b.Building.Kind == BuildingKind.Market));
        city.RemoveBuildingCommand.Execute(city.SelectedBuilding);
        Assert.False(city.HasMarket);
        Assert.False(city.OpenMarketCommand.CanExecute(null));
        Assert.False(city.BuyItemCommand.CanExecute(potion));
    });
    [Fact]
    public void MainMenu_SettingsReturnsToMainMenu() => RunSta(() =>
    {
        var context = CreateContext();
        using var city = context.City;
        context.Services.Resolve = type => type == typeof(SettingsViewModel)
            ? new SettingsViewModel(context.Navigation)
            : type == typeof(MainMenuViewModel)
                ? new MainMenuViewModel(context.Navigation, context.Saves, new DialogService()) : null;
        context.Navigation.NavigateTo<MainMenuViewModel>();
        var menu = Assert.IsType<MainMenuViewModel>(context.Navigation.CurrentViewModel);
        menu.SettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(context.Navigation.CurrentViewModel);
        settings.BackCommand.Execute(null);
        Assert.IsType<MainMenuViewModel>(context.Navigation.CurrentViewModel);
    });
    [Fact]
    public void Views_LoadWithSharedResources() => RunSta(() =>
    {
        var app = new acut.App();
        app.InitializeComponent();
        try
        {
            BattleTests.VerifyViews();
            var context = CreateContext();
            using var vm = context.City;
            for (var id = 1; id <= 6; id++)
            {
                var texture = Assert.IsAssignableFrom<BitmapSource>(app.FindResource($"BuildingTexture{id}"));
                Assert.True(texture.PixelWidth >= 512);
                Assert.True(texture.PixelHeight >= 512);
                var building = BuildingFactory.Create(FakeCatalog.Hut with { Id = id });
                building.Build();
                context.Session.City.RestoreBuilding(building);
            }
            using var mapVm = new CityViewModel(context.Navigation, context.Saves, new FakeCatalog());
            var city = new CityView { DataContext = mapVm };
            Assert.Same(app.FindResource("InventoryWeaponIcon"),
                Assert.IsType<System.Windows.Controls.Image>(city.FindName("BattleDockIcon")).Source);
            Assert.Same(app.FindResource("ProgressBookIcon"),
                Assert.IsType<System.Windows.Controls.Image>(city.FindName("ProgressDockIcon")).Source);
            city.Measure(new Size(1180, 720));
            city.Arrange(new Rect(0, 0, 1180, 720));
            city.UpdateLayout();
            mapVm.OpenProgressCommand.Execute(null);
            city.UpdateLayout();
            var progressPanel = Assert.IsType<System.Windows.Controls.Border>(city.FindName("ProgressPanel"));
            Assert.Equal(Visibility.Visible, progressPanel.Visibility);
            var progressContent = Assert.IsType<System.Windows.Controls.ContentControl>(
                Assert.IsType<System.Windows.Controls.DockPanel>(progressPanel.Child).Children[1]);
            Assert.Same(mapVm.Progress, progressContent.Content);
            var template = Assert.IsType<DataTemplate>(app.FindResource(new DataTemplateKey(typeof(ProgressViewModel))));
            var progressRoot = Assert.IsType<System.Windows.Controls.DockPanel>(template.LoadContent());
            progressRoot.DataContext = mapVm.Progress;
            var tabs = Assert.IsType<System.Windows.Controls.TabControl>(progressRoot.Children[1]);
            for (var tab = 0; tab < 3; tab++)
            {
                tabs.SelectedIndex = tab;
                progressRoot.Measure(new Size(900, 500));
                progressRoot.Arrange(new Rect(0, 0, 900, 500));
                progressRoot.UpdateLayout();
                Assert.NotNull(tabs.SelectedContent);
                var selectedTab = Assert.IsType<System.Windows.Controls.TabItem>(tabs.SelectedItem);
                Assert.Same(app.FindResource("ProgressTab"), selectedTab.Style);
                var frame = Assert.IsType<System.Windows.Controls.Border>(selectedTab.Template.FindName("Frame", selectedTab));
                Assert.Equal("#FFF1DB94", frame.BorderBrush.ToString());
            }
            mapVm.CloseProgressCommand.Execute(null);
            city.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, progressPanel.Visibility);
            var details = Assert.IsType<System.Windows.Controls.Border>(city.FindName("BuildingDetails"));
            Assert.Equal(Visibility.Collapsed, details.Visibility);
            var map = Assert.IsType<System.Windows.Controls.ScrollViewer>(city.FindName("CityMap"));
            var mapBounds = map.TransformToAncestor(city).TransformBounds(new Rect(map.RenderSize));
            var mapContent = Assert.IsType<System.Windows.Controls.ItemsControl>(map.Content);
            var firstContainer = (FrameworkElement)mapContent.ItemContainerGenerator.ContainerFromIndex(0);
            var levelText = Assert.IsType<System.Windows.Controls.TextBlock>(mapContent.ItemTemplate.FindName("BuildingLevelText", firstContainer));
            Assert.Equal("Рів. 1", levelText.Text);
            Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Disabled, map.HorizontalScrollBarVisibility);
            Assert.Equal(Visibility.Collapsed, map.ComputedHorizontalScrollBarVisibility);
            SavePreview(city, "lobby.png");
            mapVm.SelectBuildingCommand.Execute(mapVm.Buildings.First());
            city.UpdateLayout();
            Assert.Equal(Visibility.Visible, details.Visibility);
            Assert.True(mapBounds.Right <= details.TransformToAncestor(city).Transform(new Point()).X);
            Assert.Equal(mapBounds, map.TransformToAncestor(city).TransformBounds(new Rect(map.RenderSize)));
            mapVm.UpgradeCommand.Execute(mapVm.SelectedBuilding);
            city.UpdateLayout();
            firstContainer = (FrameworkElement)mapContent.ItemContainerGenerator.ContainerFromIndex(0);
            levelText = Assert.IsType<System.Windows.Controls.TextBlock>(mapContent.ItemTemplate.FindName("BuildingLevelText", firstContainer));
            Assert.Equal("Рів. 2", levelText.Text);
            Assert.Equal(90, mapVm.SelectedBuilding!.DemolitionRefund);
            SavePreview(city, "lobby-selected.png");
            mapVm.CloseDetailsCommand.Execute(null);
            mapVm.ToggleConstructionCommand.Execute(null);
            city.UpdateLayout();
            var construction = Assert.IsType<System.Windows.Controls.Border>(city.FindName("ConstructionPanel"));
            Assert.True(mapBounds.Right <= construction.TransformToAncestor(city).Transform(new Point()).X);
            Assert.Equal(mapBounds, map.TransformToAncestor(city).TransformBounds(new Rect(map.RenderSize)));
            SavePreview(city, "lobby-construction.png");

            city.Measure(new Size(900, 600));
            city.Arrange(new Rect(0, 0, 900, 600));
            city.UpdateLayout();
            var compactMap = map.TransformToAncestor(city).TransformBounds(new Rect(map.RenderSize));
            Assert.True(compactMap.Right <= construction.TransformToAncestor(city).Transform(new Point()).X);
            Assert.True(construction.TransformToAncestor(city).TransformBounds(new Rect(construction.RenderSize)).Right <= 900);
            Assert.Equal(Visibility.Collapsed, map.ComputedHorizontalScrollBarVisibility);
            Assert.True(map.ExtentWidth <= map.ViewportWidth + 0.01);
            Assert.Equal(CityViewModel.PlotWidth, ((FrameworkElement)mapContent.ItemContainerGenerator.ContainerFromIndex(0)).ActualWidth);
            SavePreview(city, "lobby-compact.png");
            city.Measure(new Size(1180, 720));
            city.Arrange(new Rect(0, 0, 1180, 720));
            city.UpdateLayout();
            mapVm.TogglePauseCommand.Execute(null);
            city.UpdateLayout();
            SavePreview(city, "lobby-paused.png");
            mapVm.ResumeCommand.Execute(null);
            context.Session.Player.AddGold(10000);
            var plotWidth = ((FrameworkElement)mapContent.ItemContainerGenerator.ContainerFromIndex(0)).ActualWidth;
            while (mapVm.BuildingCount < mapVm.BuildingLimit)
                mapVm.BuildCommand.Execute(mapVm.BuildOptions.First());
            city.UpdateLayout();
            Assert.True(map.ExtentWidth <= map.ViewportWidth + 0.01);
            Assert.Equal(Visibility.Collapsed, map.ComputedHorizontalScrollBarVisibility);
            Assert.Equal(plotWidth, ((FrameworkElement)mapContent.ItemContainerGenerator.ContainerFromIndex(0)).ActualWidth);
            Assert.Equal(CityViewModel.PlotWidth, plotWidth);
            Assert.True(map.ExtentHeight > map.ViewportHeight);
            map.ScrollToBottom();
            city.UpdateLayout();
            Assert.True(map.VerticalOffset > 0);
            SavePreview(city, "lobby-full-scrolled.png");
            var economyContext = CreateContext(new ResourceCatalog());
            using var economyVm = economyContext.City;
            economyContext.Session.Player.AddResources(new(5000, 5000, 5000));
            foreach (var typeId in new[] { 1, 4, 5, 6 })
                economyVm.BuildCommand.Execute(economyVm.BuildOptions.Single(o => o.Type.Id == typeId));
            var economyView = new CityView { DataContext = economyVm };
            economyView.Measure(new Size(1180, 720));
            economyView.Arrange(new Rect(0, 0, 1180, 720));
            economyView.UpdateLayout();
            SavePreview(economyView, "resource-lobby.png");
            economyVm.OpenMarketCommand.Execute(null);
            economyView.UpdateLayout();
            Assert.Equal(Visibility.Visible, ((System.Windows.Controls.Border)economyView.FindName("MarketPanel")).Visibility);
            SavePreview(economyView, "resource-market.png");
            var ingredient = economyVm.MarketItems.Single(i => i.Id == FakeCatalog.SmallHealPotion.Id);
            economyVm.BuyItemCommand.Execute(ingredient);
            economyVm.BuyItemCommand.Execute(ingredient);
            economyVm.OpenCraftingCommand.Execute(null);
            economyView.UpdateLayout();
            Assert.Equal(Visibility.Visible, ((System.Windows.Controls.Border)economyView.FindName("CraftingPanel")).Visibility);
            SavePreview(economyView, "resource-crafting.png");
            var sword = FakeCatalog.IronSword.Clone();
            var armor = FakeCatalog.LeatherArmor.Clone();
            economyContext.Session.Player.Inventory.AddItem(sword);
            economyContext.Session.Player.Inventory.AddItem(armor);
            var inventoryVm = new InventoryViewModel(economyContext.Navigation, economyContext.Saves);
            var inventoryView = new InventoryView { DataContext = inventoryVm };
            inventoryView.Measure(new Size(1180, 720));
            inventoryView.Arrange(new Rect(0, 0, 1180, 720));
            inventoryView.UpdateLayout();
            Assert.Null(inventoryView.FindName("ShopPanel"));
            Assert.Null(inventoryView.FindName("ShopList"));
            Assert.Null(inventoryView.FindName("GoldBalance"));
            Assert.Null(inventoryView.FindName("UpgradeButton"));
            Assert.Null(inventoryView.FindName("UpgradePrice"));
            Assert.False(inventoryVm.HasSelection);
            Assert.Equal(GameState.Paused, economyContext.Session.CurrentState);
            var pauseNotice = (System.Windows.Controls.TextBlock)inventoryView.FindName("PauseNotice");
            Assert.Contains("Місто на паузі", pauseNotice.Text);
            var inventoryList = Assert.IsType<System.Windows.Controls.ItemsControl>(inventoryView.FindName("InventoryList"));
            Assert.Equal(inventoryVm.Capacity, inventoryList.Items.Count);
            var firstSlot = (FrameworkElement)inventoryList.ItemContainerGenerator.ContainerFromIndex(0);
            var secondSlot = (FrameworkElement)inventoryList.ItemContainerGenerator.ContainerFromIndex(1);
            var nextRow = (FrameworkElement)inventoryList.ItemContainerGenerator.ContainerFromIndex(8);
            Assert.Equal(firstSlot.TranslatePoint(new Point(), inventoryView).Y, secondSlot.TranslatePoint(new Point(), inventoryView).Y);
            Assert.True(nextRow.TranslatePoint(new Point(), inventoryView).Y > firstSlot.TranslatePoint(new Point(), inventoryView).Y);
            var weaponIndex = inventoryVm.Slots.Single(s => s.Stack?.Item.Id == sword.Id).Index;
            var weaponContainer = (FrameworkElement)inventoryList.ItemContainerGenerator.ContainerFromIndex(weaponIndex);
            var selectButton = Assert.IsType<System.Windows.Controls.Button>(inventoryList.ItemTemplate.FindName("SlotButton", weaponContainer));
            Assert.Same(inventoryVm.SelectItemCommand, selectButton.Command);
            selectButton.Command.Execute(selectButton.CommandParameter);
            inventoryView.UpdateLayout();
            Assert.Same(sword, inventoryVm.SelectedItem!.Item);
            var equipButton = (System.Windows.Controls.Button)inventoryView.FindName("EquipButton");
            Assert.Same(inventoryVm.EquipCommand, equipButton.Command);
            Assert.Same(sword, equipButton.CommandParameter);
            equipButton.Command.Execute(equipButton.CommandParameter);
            inventoryVm.EquipCommand.Execute(armor);
            Pump(TimeSpan.FromMilliseconds(10));
            inventoryView.UpdateLayout();
            var weaponSlot = (System.Windows.Controls.Button)inventoryView.FindName("WeaponSlot");
            var armorSlot = (System.Windows.Controls.Button)inventoryView.FindName("ArmorSlot");
            Assert.Same(sword, ((System.Windows.Controls.ContentControl)weaponSlot.Content).Content);
            Assert.Same(armor, ((System.Windows.Controls.ContentControl)armorSlot.Content).Content);
            Assert.Same(inventoryVm.UnequipWeaponCommand, weaponSlot.Command);
            Assert.Same(inventoryVm.UnequipArmorCommand, armorSlot.Command);
            Assert.False(equipButton.IsEnabled);
            Assert.Equal("Вдягнуто", equipButton.Content);
            Assert.True(weaponSlot.TranslatePoint(new Point(), inventoryView).X < 200);
            Assert.True(armorSlot.TranslatePoint(new Point(), inventoryView).X < 200);
            SavePreview(inventoryView, "inventory.png");
            inventoryView.Measure(new Size(900, 600));
            inventoryView.Arrange(new Rect(0, 0, 900, 600));
            inventoryView.UpdateLayout();
            var ownedPanel = (FrameworkElement)inventoryView.FindName("OwnedItemsPanel");
            var detailsPanel = (FrameworkElement)inventoryView.FindName("ItemDetailsPanel");
            var ownedBounds = ownedPanel.TransformToAncestor(inventoryView).TransformBounds(new Rect(ownedPanel.RenderSize));
            Assert.True(ownedBounds.Bottom <= detailsPanel.TranslatePoint(new Point(), inventoryView).Y);
            Assert.True(ownedBounds.Right <= 900);
            var detailsBounds = detailsPanel.TransformToAncestor(inventoryView).TransformBounds(new Rect(detailsPanel.RenderSize));
            var equipBounds = equipButton.TransformToAncestor(inventoryView).TransformBounds(new Rect(equipButton.RenderSize));
            Assert.True(equipBounds.Height > 0);
            Assert.True(equipBounds.Top >= detailsBounds.Top && equipBounds.Bottom <= detailsBounds.Bottom);
            SavePreview(inventoryView, "inventory-compact.png");
            weaponSlot.Command.Execute(null);
            inventoryView.UpdateLayout();
            Assert.Null(((System.Windows.Controls.ContentControl)weaponSlot.Content).Content);
            Assert.Null(inventoryVm.EquippedWeapon);
            var inventoryAppearance = (System.Windows.Controls.ContentControl)inventoryView.FindName("PlayerAppearance");
            var cityAppearance = (System.Windows.Controls.ContentControl)city.FindName("PlayerAppearance");
            Assert.Equal(inventoryVm.Appearance, inventoryAppearance.Content);
            Assert.Equal(mapVm.Appearance, cityAppearance.Content);
            var menuVm = new MainMenuViewModel(context.Navigation, context.Saves, new DialogService());
            var menu = new MainMenuView { DataContext = menuVm };
            menu.Measure(new Size(1180, 720));
            menu.Arrange(new Rect(0, 0, 1180, 720));
            menu.UpdateLayout();
            Assert.False(menuVm.ContinueCommand.CanExecute(null));
            SavePreview(menu, "main-menu.png");
            var character = new CharacterCreationView
            {
                DataContext = new CharacterCreationViewModel(context.Navigation, context.Saves, new FakeCatalog(), new DialogService())
            };
            character.Measure(new Size(1180, 720));
            character.Arrange(new Rect(0, 0, 1180, 720));
            character.UpdateLayout();
            var creation = (CharacterCreationViewModel)character.DataContext;
            var hairChoice = (System.Windows.Controls.ComboBox)character.FindName("HairChoice");
            var skinChoice = (System.Windows.Controls.ComboBox)character.FindName("SkinChoice");
            var outfitChoice = (System.Windows.Controls.ComboBox)character.FindName("OutfitChoice");
            Assert.Equal("Short", hairChoice.SelectedValue);
            Assert.Equal("Коротка", ((AppearanceOption)hairChoice.SelectedItem).Label);
            Assert.Equal("Label", hairChoice.DisplayMemberPath);
            hairChoice.SelectedValue = "Braid";
            skinChoice.SelectedValue = "Dark";
            outfitChoice.SelectedValue = "Knight";
            character.UpdateLayout();
            Assert.Equal("Коса", ((AppearanceOption)hairChoice.SelectedItem).Label);
            Assert.Equal("Темний", ((AppearanceOption)skinChoice.SelectedItem).Label);
            Assert.Equal("Лицар", ((AppearanceOption)outfitChoice.SelectedItem).Label);
            var preview = (System.Windows.Controls.ContentControl)character.FindName("AppearancePreview");
            Assert.Equal(new Acut.Core.Domain.Characters.AppearanceSettings("Braid", "Dark", "Knight"), preview.Content);
            Assert.Equal(creation.PreviewAppearance, preview.Content);
            Assert.Same(preview.ContentTemplate, inventoryAppearance.ContentTemplate);
            Assert.Same(preview.ContentTemplate, cityAppearance.ContentTemplate);
            SavePreview(character, "character-creation.png");
            var signatures = new HashSet<string>();
            foreach (var hair in creation.HairStyles)
            foreach (var skin in creation.SkinColors)
            foreach (var outfit in creation.Outfits)
            {
                var appearance = new Acut.Core.Domain.Characters.AppearanceSettings(hair.Value, skin.Value, outfit.Value);
                var visual = new System.Windows.Controls.ContentControl
                {
                    Content = appearance, ContentTemplate = preview.ContentTemplate,
                    Style = (Style)app.FindResource("CharacterVisual")
                };
                visual.Measure(new Size(150, 280));
                visual.Arrange(new Rect(0, 0, 150, 280));
                visual.UpdateLayout();
                var bitmap = new RenderTargetBitmap(150, 280, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var pixels = new byte[150 * 280 * 4];
                bitmap.CopyPixels(pixels, 150 * 4, 0);
                Assert.True(signatures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))),
                    $"Зовнішність {hair}/{skin}/{outfit} повинна мати окремий вигляд.");
            }
            Assert.Equal(27, signatures.Count);
            character.Measure(new Size(900, 600));
            character.Arrange(new Rect(0, 0, 900, 600));
            character.UpdateLayout();
            var previewBounds = preview.TransformToAncestor(character).TransformBounds(new Rect(preview.RenderSize));
            Assert.True(previewBounds.Width > 0 && previewBounds.Height > 0);
            Assert.True(previewBounds.Right <= 900 && previewBounds.Bottom <= 600);
            SavePreview(character, "character-creation-compact.png");
        }
        finally { app.Shutdown(); }
    });

    private static void SavePreview(FrameworkElement element, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("ACUT_UI_PREVIEW_PATH");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(directory, fileName));
        encoder.Save(file);
    }

    private static Context CreateContext(ICatalogRepository? catalog = null)
    {
        var repository = new MemorySaveRepository();
        var saves = new SaveManager(repository);
        catalog ??= new FakeCatalog();
        var session = GameSession.StartNewGame("Марко", null, catalog);
        saves.Register(session);
        var services = new TestServices();
        var navigation = new NavigationService(services);
        services.Resolve = type => type == typeof(MainMenuViewModel)
            ? new MainMenuViewModel(navigation, saves, new DialogService()) : null;
        return new Context(repository, saves, session, services, navigation,
            new CityViewModel(navigation, saves, catalog));
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

    private sealed class ResourceCatalog : ICatalogRepository
    {
        private readonly FakeCatalog _inner = new();
        public NewGameEconomy GetNewGameEconomy() => new(new(0, 150, 0), 3);
        public IReadOnlyList<BuildingType> GetBuildingTypes() =>
        [
            FakeCatalog.Hut with { BuildCost = 0, BuildWoodCost = 100, UpgradeCost = 0, UpgradeWoodCost = 80 },
            FakeCatalog.Hut with { Id = 3, Name = "Лісопил", Kind = BuildingKind.Production, Resource = "Wood", ProductionRate = 4, BuildCost = 0, BuildWoodCost = 120 },
            FakeCatalog.Hut with { Id = 4, Name = "Шахта", Kind = BuildingKind.Production, Resource = "Stone", ProductionRate = 4, BuildCost = 0, BuildWoodCost = 150 },
            FakeCatalog.Workshop with { Id = 5, MaxLevel = 1, BuildCost = 100, BuildWoodCost = 120, BuildStoneCost = 100 },
            FakeCatalog.Workshop with { Id = 6, Name = "Ринок", Kind = BuildingKind.Market, MaxLevel = 1, BuildCost = 100, BuildWoodCost = 150, BuildStoneCost = 150 }
        ];
        public IReadOnlyList<Acut.Core.Domain.Items.Item> GetItems() => _inner.GetItems();
        public IReadOnlyList<Acut.Core.Domain.Items.Recipe> GetRecipes() => _inner.GetRecipes();
        public IReadOnlyList<Acut.Core.Domain.Progress.Skill> GetSkills() => _inner.GetSkills();
        public IReadOnlyList<Acut.Core.Domain.Progress.Quest> GetQuests() => _inner.GetQuests();
        public IReadOnlyList<Acut.Core.Domain.Progress.Achievement> GetAchievements() => _inner.GetAchievements();
        public IReadOnlyList<Acut.Core.Domain.Combat.EnemyDefinition> GetEnemies() => _inner.GetEnemies();
        public IReadOnlyList<Stock> GetStocks() => _inner.GetStocks();
    }
    private sealed class MemorySaveRepository : ISaveRepository
    {
        public int SaveCount { get; private set; }
        public GameState SavedState { get; private set; }
        public bool FailSave { get; set; }
        private GameSession? _saved;
        public void Save(GameSession session)
        {
            if (FailSave) throw new InvalidOperationException("Помилка тестового сховища.");
            SaveCount++;
            SavedState = session.CurrentState;
            _saved = new GameSession(session.SessionId, session.StartedAt, session.CurrentState,
                session.Player, session.City, new GameEventDispatcher());
        }
        public GameSession? Load(Guid saveId) => _saved?.SessionId == saveId ? _saved : null;
        public IReadOnlyList<SaveSlotInfo> ListSaves() => _saved is null
            ? [] : [new SaveSlotInfo(_saved.SessionId, _saved.Player.Name, DateTime.UtcNow)];
        public void Delete(Guid saveId) { }
    }
}
