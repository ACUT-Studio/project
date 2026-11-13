using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class InventoryAndItemsTests
{
    [Fact]
    public void Inventory_AddAndRemoveItems_ManagesStacksAndCapacity()
    {
        var inventory = new Inventory(capacity: 2);
        var potion = FakeCatalog.SmallHealPotion.Clone();
        var sword = FakeCatalog.IronSword.Clone();
        var armor = FakeCatalog.LeatherArmor.Clone();

        Assert.True(inventory.CanAddItem(potion, 3));
        Assert.True(inventory.AddItem(potion, 3));
        Assert.True(inventory.AddItem(sword, 1));

        // Третій новий предмет не вміщується, але існуючий стак можна поповнити
        Assert.False(inventory.CanAddItem(armor, 1));
        Assert.False(inventory.AddItem(armor, 1));
        Assert.True(inventory.CanAddItem(potion, 2));
        Assert.True(inventory.AddItem(potion, 2));
        Assert.Equal(5, inventory.GetQuantity(potion));

        // Часткове та повне видалення зі стаку
        Assert.False(inventory.RemoveItem(potion, 10));
        Assert.True(inventory.RemoveItem(potion, 2));
        Assert.Equal(3, inventory.GetQuantity(potion));
        Assert.True(inventory.RemoveItem(potion, 3));
        Assert.False(inventory.HasItem(potion));
        Assert.Single(inventory.GetItems());
    }

    [Fact]
    public void Items_UpgradeAndRestoreLevel_ScalesBonusesAndRespectsMaxLevel()
    {
        var weapon = new Weapon(10, "Меч", "Гострий", 100, bonusAttack: 4);
        var armor = new Armor(11, "Лати", "Міцні", 120, bonusDefense: 3);

        Assert.True(weapon.Upgrade());
        Assert.Equal(2, weapon.Level);
        Assert.Equal(8, weapon.BonusAttack);
        Assert.Equal(200, weapon.UpgradeCost);

        armor.RestoreLevel(GameConfig.MaxItemLevel);
        Assert.Equal(GameConfig.MaxItemLevel, armor.Level);
        Assert.Equal(3 * GameConfig.MaxItemLevel, armor.BonusDefense);
        Assert.False(armor.CanUpgrade);
        Assert.False(armor.Upgrade());

        // Рівність предметів визначається за Id
        var clonedWeapon = weapon.Clone();
        Assert.Equal(weapon, clonedWeapon);
        Assert.Equal(weapon.GetHashCode(), clonedWeapon.GetHashCode());
    }

    [Fact]
    public void Potion_Use_HealsTargetAndThrowsOnInvalidEffect()
    {
        var player = new Player("Герой", AppearanceSettings.Default, health: 40, maxHealth: 100);
        var healPotion = new Potion(1, "Зілля", "", 30, PotionEffect.Heal, 35);
        healPotion.Use(player);
        Assert.Equal(75, player.Health);

        var invalidPotion = new Potion(2, "Невідоме", "", 30, (PotionEffect)99, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidPotion.Use(player));
    }

    [Fact]
    public void City_UpgradeBuildingByObjectAndGuid_ReachesMaxLevelAndPublishesEvents()
    {
        var player = new Player("Мер", AppearanceSettings.Default, gold: 1000);
        var dispatcher = new GameEventDispatcher();
        var upgradedEvents = 0;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.BuildingUpgraded) upgradedEvents++;
        });

        var city = new City("Місто", player, dispatcher);
        var building = city.BuildBuilding(FakeCatalog.Hut); // MaxLevel = 3, UpgradeCost = 80

        Assert.True(city.UpgradeBuilding(building.Id));
        Assert.Equal(2, building.Level);
        Assert.Equal(BuildingStatus.Active, building.Status);

        Assert.True(city.UpgradeBuilding(building));
        Assert.Equal(3, building.Level);
        Assert.Equal(BuildingStatus.MaxLevel, building.Status);
        Assert.Equal(2, upgradedEvents);

        // Подальше покращення на MaxLevel неможливе
        Assert.False(city.CanUpgradeBuilding(building, out var reason));
        Assert.NotNull(reason);
        Assert.Throws<GameRuleException>(() => city.UpgradeBuilding(building));
        Assert.Throws<GameRuleException>(() => city.UpgradeBuilding(Guid.NewGuid()));
    }
}