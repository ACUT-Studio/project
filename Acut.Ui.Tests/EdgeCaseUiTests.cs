using acut.ViewModels;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Acut.Tests.Core;
using Acut.Ui.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class EdgeCaseUiTests
{
    [Fact]
    public void InventoryViewModel_OpenedWhenCityAlreadyPaused_DoesNotResumeCityOnBack()
    {
        using var ctx = new UiTestContext();
        var session = ctx.StartAndRegisterSession();

        // Місто вже було поставлено на паузу гравцем до відкриття інвентаря
        session.ChangeState(GameState.Paused);

        var inventoryVm = ctx.Services.GetRequiredService<InventoryViewModel>();
        Assert.True(inventoryVm.IsCityPaused);

        // Повернення назад НЕ повинно знімати паузу, оскільки _resumeOnBack == false
        inventoryVm.BackCommand.Execute(null);
        Assert.Equal(GameState.Paused, session.CurrentState);
    }

    [Fact]
    public void CityViewModel_PlotsGridBoundary_ExpandsAt19thBuildingAndShrinksBackAt18th()
    {
        using var ctx = new UiTestContext(new ResourceAmounts(10000, 10000, 10000));
        ctx.StartAndRegisterSession();
        using var vm = ctx.Services.GetRequiredService<CityViewModel>();

        var hutOption = vm.BuildOptions.First(o => o.Type.Id == UiFakeCatalog.Hut.Id);

        // Початково є 1 будівля; будуємо ще 17 (разом 18 — рівно 3 рядки по 6)
        for (var i = vm.BuildingCount; i < 18; i++)
            vm.BuildCommand.Execute(hutOption);

        Assert.Equal(18, vm.BuildingCount);
        Assert.Equal(18, vm.Plots.Count);

        // 19-та будівля переходить межу 18 і додає новий рядок (24 ділянки)
        vm.BuildCommand.Execute(hutOption);
        Assert.Equal(19, vm.BuildingCount);
        Assert.Equal(24, vm.Plots.Count);

        // Знесення 19-ї будівлі стискає сітку назад до 18 ділянок
        vm.RemoveBuildingCommand.Execute(vm.Buildings.Last());
        Assert.Equal(18, vm.BuildingCount);
        Assert.Equal(18, vm.Plots.Count);
    }

    [Fact]
    public void CityViewModel_DemolishingWorkshopOrMarketWhilePanelIsOpen_AutomaticallyClosesPanel()
    {
        using var ctx = new UiTestContext(new ResourceAmounts(5000, 5000, 5000));
        ctx.StartAndRegisterSession();
        using var vm = ctx.Services.GetRequiredService<CityViewModel>();

        vm.BuildCommand.Execute(vm.BuildOptions.First(o => o.Type.Id == UiFakeCatalog.Workshop.Id));
        vm.OpenCraftingCommand.Execute(null);
        Assert.True(vm.IsCraftingOpen);

        var workshopRow = vm.Buildings.Single(b => b.TypeId == UiFakeCatalog.Workshop.Id);
        vm.RemoveBuildingCommand.Execute(workshopRow);

        Assert.False(vm.HasWorkshop);
        Assert.False(vm.IsCraftingOpen);
    }

    [Fact]
    public void CityViewModel_ExitToMenuWhenSaveThrows_LeavesCityPausedAndSetsStatusMessage()
    {
        using var ctx = new UiTestContext();
        var session = ctx.StartAndRegisterSession();
        using var vm = ctx.Services.GetRequiredService<CityViewModel>();

        ctx.SaveRepository.ThrowOnSave = true;
        vm.ExitToMenuCommand.Execute(null);

        Assert.Equal(GameState.Paused, session.CurrentState);
        Assert.True(vm.IsPaused);
        Assert.Contains("Не вдалося вийти в меню", vm.StatusMessage);
    }

    [Fact]
    public void VictoryViewModel_WhenDefeatLoadedWithoutPenaltyValue_ShowsFallbackPenaltyText()
    {
        using var ctx = new UiTestContext();
        var session = ctx.StartAndRegisterSession();
        var battle = session.StartBattle(new Enemy("Бос", EnemyType.Boss, 100, 500, 10, 100, 100, new AggressiveStrategy()));

        // Імітуємо завантаження збереження одразу в стані Defeat (коли LastDefeatPenalty == null)
        battle.Restore(turnNumber: 3, isPlayerTurn: true, finished: true);
        session.ChangeState(GameState.Defeat);

        var vm = ctx.Services.GetRequiredService<VictoryViewModel>();

        Assert.False(vm.IsVictory);
        Assert.Equal("ПОРАЗКА", vm.Title);
        Assert.Equal("Штраф до золота застосовано", vm.RewardGold);
    }

    [Fact]
    public void BattleAndVictoryViewModels_WithoutActiveSessionOrBattle_ThrowInvalidOperationException()
    {
        using var ctx = new UiTestContext();

        // Спроба створити BattleViewModel або VictoryViewModel без розпочатої сесії
        Assert.Throws<InvalidOperationException>(() => ctx.Services.GetRequiredService<BattleViewModel>());
        Assert.Throws<InvalidOperationException>(() => ctx.Services.GetRequiredService<VictoryViewModel>());

        // Сесія у стані City (немає активного бою або результату бою)
        ctx.StartAndRegisterSession();
        Assert.Throws<InvalidOperationException>(() => ctx.Services.GetRequiredService<BattleViewModel>());
        Assert.Throws<InvalidOperationException>(() => ctx.Services.GetRequiredService<VictoryViewModel>());
    }
}