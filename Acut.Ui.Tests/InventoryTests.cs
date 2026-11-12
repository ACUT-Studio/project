using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Acut.Core.Abstractions;
using Acut.Core.Domain.Items;
using Acut.Core.Services;
using Acut.Tests.Core;
using acut.Services;
using acut.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Acut.Ui.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void Slots_RepresentCapacityAndOnlyOwnedItems() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        session.Player.Inventory.AddItem(FakeCatalog.IronSword.Clone(), 2);
        session.Player.Inventory.AddItem(FakeCatalog.LeatherArmor.Clone());
        var vm = services.GetRequiredService<InventoryViewModel>();
        Assert.Equal(session.Player.Inventory.Capacity, vm.Slots.Count);
        Assert.Equal(2, vm.Slots.Count(s => s.IsOccupied));
        Assert.Equal(2, vm.Slots[0].Stack!.Quantity);
        Assert.Same(vm.Items[0], vm.Slots[0].Stack);
        Assert.All(vm.Slots.Skip(2), s => Assert.Null(s.Stack));
        Assert.False(vm.HasSelection);
        Assert.False(vm.SelectItemCommand.CanExecute(vm.Slots[^1]));
        vm.SelectItemCommand.Execute(vm.Slots[^1]);
        Assert.Null(vm.SelectedItem);
    });

    [Fact]
    public void SelectionAndEquipment_UseRealItemsAndRefreshBonusesWithoutPayment() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        var sword = (Weapon)FakeCatalog.IronSword.Clone();
        var armor = (Armor)FakeCatalog.LeatherArmor.Clone();
        session.Player.Inventory.AddItem(sword);
        session.Player.Inventory.AddItem(armor);
        var vm = services.GetRequiredService<InventoryViewModel>();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        var gold = session.Player.Gold;
        vm.SelectItemCommand.Execute(vm.Slots[0]);
        Assert.True(vm.IsEquipmentSelected);
        vm.EquipCommand.Execute(vm.SelectedItem!.Item);
        Assert.Same(sword, vm.EquippedWeapon);
        Assert.Equal(session.Player.AttackPower + sword.BonusAttack, vm.PlayerAttack);
        Assert.True(vm.IsSelectedEquipped);
        Assert.False(vm.EquipCommand.CanExecute(sword));
        Assert.Same(sword, vm.SelectedItem.Item);
        Assert.Contains(nameof(vm.EquippedWeapon), notified);
        Assert.Contains(nameof(vm.PlayerAttack), notified);
        vm.SelectItemCommand.Execute(vm.Slots[1]);
        vm.EquipCommand.Execute(vm.SelectedItem!.Item);
        Assert.Same(armor, vm.EquippedArmor);
        Assert.Equal(session.Player.Defense + armor.BonusDefense, vm.PlayerDefense);
        Assert.Equal(gold, session.Player.Gold);
        Assert.Equal(1, sword.Level);
        Assert.Equal(1, armor.Level);
        Assert.Single(vm.Slots, s => s.IsSelected);
    });

    [Fact]
    public void Unequip_ClearsSlotsAndRestoresBaseStats() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        var sword = FakeCatalog.IronSword.Clone();
        var armor = FakeCatalog.LeatherArmor.Clone();
        session.Player.Inventory.AddItem(sword);
        session.Player.Inventory.AddItem(armor);
        session.Player.EquipItem(sword);
        session.Player.EquipItem(armor);
        var vm = services.GetRequiredService<InventoryViewModel>();
        vm.UnequipWeaponCommand.Execute(null);
        vm.UnequipArmorCommand.Execute(null);
        Assert.Null(vm.EquippedWeapon);
        Assert.Null(vm.EquippedArmor);
        Assert.Equal(session.Player.AttackPower, vm.PlayerAttack);
        Assert.Equal(session.Player.Defense, vm.PlayerDefense);
        Assert.False(vm.UnequipWeaponCommand.CanExecute(null));
        Assert.False(vm.UnequipArmorCommand.CanExecute(null));
        Assert.Equal(2, vm.UsedSlots);
    });

    [Fact]
    public void PotionsAndUnownedEquipment_CannotBeEquipped() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        session.Player.Inventory.AddItem(FakeCatalog.SmallHealPotion.Clone());
        var vm = services.GetRequiredService<InventoryViewModel>();
        vm.SelectItemCommand.Execute(vm.Slots[0]);
        Assert.False(vm.IsEquipmentSelected);
        Assert.False(vm.EquipCommand.CanExecute(vm.SelectedItem!.Item));
        Assert.False(vm.EquipCommand.CanExecute(FakeCatalog.IronSword));
        Assert.False(vm.EquipCommand.CanExecute(null));
        vm.EquipCommand.Execute(FakeCatalog.IronSword);
        Assert.Null(vm.EquippedWeapon);
    });

    [Fact]
    public void OpeningInventory_PausesCityAndStopsResourceProduction() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        session.City.BuildBuilding(FakeCatalog.Hut);
        var vm = services.GetRequiredService<InventoryViewModel>();
        var resources = session.Player.Resources;
        session.Tick(TimeSpan.FromSeconds(1));
        Assert.True(vm.IsCityPaused);
        Assert.Equal(GameState.Paused, session.CurrentState);
        Assert.Contains("Місто на паузі", vm.PauseMessage);
        Assert.Equal(resources, session.Player.Resources);
        Assert.True(vm.IsEmpty);
        Assert.All(vm.Slots, s => Assert.False(s.IsOccupied));
    });

    [Fact]
    public void Back_ResumesCityAndPreservesEquipment() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        var navigation = services.GetRequiredService<NavigationService>();
        var sword = FakeCatalog.IronSword.Clone();
        session.Player.Inventory.AddItem(sword);
        navigation.NavigateTo<CityViewModel>();
        var city = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
        city.OpenInventoryCommand.Execute(null);
        var inventory = Assert.IsType<InventoryViewModel>(navigation.CurrentViewModel);
        Assert.Equal(GameState.Paused, session.CurrentState);
        inventory.EquipCommand.Execute(sword);
        inventory.BackCommand.Execute(null);
        using var returnedCity = Assert.IsType<CityViewModel>(navigation.CurrentViewModel);
        Assert.Equal(GameState.City, session.CurrentState);
        Assert.False(returnedCity.IsPaused);
        Assert.Same(session, services.GetRequiredService<SaveManager>().Current);
        Assert.Same(sword, session.Player.EquippedWeapon);
    });

    [Fact]
    public void Back_WhenAlreadyPaused_PreservesOriginalPause() => RunSta(() =>
    {
        using var services = CreateServices(out var session);
        session.ChangeState(GameState.Paused);
        var vm = services.GetRequiredService<InventoryViewModel>();
        vm.BackCommand.Execute(null);
        using var city = Assert.IsType<CityViewModel>(services.GetRequiredService<NavigationService>().CurrentViewModel);
        Assert.True(city.IsPaused);
    });

    private static ServiceProvider CreateServices(out GameSession session)
    {
        var catalog = new FakeCatalog();
        session = GameSession.StartNewGame("Марко", null, catalog);
        var saves = new SaveManager(new UnusedSaveRepository());
        saves.Register(session);
        return new ServiceCollection().AddSingleton<ICatalogRepository>(catalog).AddSingleton(saves)
            .AddSingleton<NavigationService>().AddTransient<InventoryViewModel>().AddTransient<CityViewModel>()
            .BuildServiceProvider();
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

    private sealed class UnusedSaveRepository : ISaveRepository
    {
        public void Save(GameSession session) => throw new NotSupportedException();
        public GameSession? Load(Guid saveId) => throw new NotSupportedException();
        public IReadOnlyList<SaveSlotInfo> ListSaves() => throw new NotSupportedException();
        public void Delete(Guid saveId) => throw new NotSupportedException();
    }
}
