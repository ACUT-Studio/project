using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Xunit;

namespace Acut.Tests.Core;

public sealed class EquipmentTests
{
    [Fact]
    public void Equip_OwnedWeaponAndArmor_AddsBonusesWithoutChangingBaseStatsOrInventory()
    {
        var player = new Player("Тест", AppearanceSettings.Default, gold: 100);
        var weapon = (Weapon)FakeCatalog.IronSword.Clone();
        var armor = (Armor)FakeCatalog.LeatherArmor.Clone();
        player.Inventory.AddItem(weapon, 2);
        player.Inventory.AddItem(armor);
        var attack = player.AttackPower;
        var defense = player.Defense;

        Assert.True(player.EquipItem(weapon));
        Assert.True(player.EquipItem(armor));
        Assert.Same(weapon, player.EquippedWeapon);
        Assert.Same(armor, player.EquippedArmor);
        Assert.Equal(attack + weapon.BonusAttack, player.TotalAttackPower);
        Assert.Equal(defense + armor.BonusDefense, player.TotalDefense);
        Assert.Equal(attack, player.AttackPower);
        Assert.Equal(defense, player.Defense);
        Assert.Equal(100, player.Gold);
        Assert.Equal(2, player.Inventory.GetQuantity(weapon));
        Assert.Equal(1, weapon.Level);
    }

    [Fact]
    public void Equip_ReplacementAndRepeatedEquip_DoNotAccumulateBonuses()
    {
        var player = new Player("Тест", AppearanceSettings.Default);
        var first = (Weapon)FakeCatalog.IronSword.Clone();
        var second = new Weapon(100, "Інший меч", "", 10, 12);
        player.Inventory.AddItem(first);
        player.Inventory.AddItem(second);
        player.EquipItem(first);
        player.EquipItem(first);
        Assert.Equal(player.AttackPower + first.BonusAttack, player.TotalAttackPower);
        player.EquipItem(second);
        Assert.Same(second, player.EquippedWeapon);
        Assert.Equal(player.AttackPower + second.BonusAttack, player.TotalAttackPower);
        player.UnequipWeapon();
        Assert.Null(player.EquippedWeapon);
        Assert.Equal(player.AttackPower, player.TotalAttackPower);
        Assert.Equal(2, player.Inventory.GetItems().Count);
    }

    [Fact]
    public void Equip_RejectsUnownedItemsAndPotions()
    {
        var player = new Player("Тест", AppearanceSettings.Default);
        player.Inventory.AddItem(FakeCatalog.SmallHealPotion.Clone());
        Assert.False(player.EquipItem(FakeCatalog.IronSword));
        Assert.False(player.EquipItem(FakeCatalog.SmallHealPotion));
        Assert.Null(player.EquippedWeapon);
        Assert.Null(player.EquippedArmor);
    }

    [Fact]
    public void Equip_UsesOwnedInstanceAndLosesBonusWhenItemIsRemoved()
    {
        var player = new Player("Тест", AppearanceSettings.Default);
        var armor = (Armor)FakeCatalog.LeatherArmor.Clone();
        player.Inventory.AddItem(armor);
        var forged = new Armor(armor.Id, "Інша броня", "", 1, 999);
        Assert.True(player.EquipItem(forged));
        Assert.Same(armor, player.EquippedArmor);
        Assert.Equal(player.Defense + armor.BonusDefense, player.TotalDefense);
        player.Inventory.RemoveItem(armor);
        Assert.Null(player.EquippedArmor);
        Assert.Equal(player.Defense, player.TotalDefense);
    }

    [Fact]
    public void LevelUpAndUnequip_PreserveBaseProgress()
    {
        var player = new Player("Тест", AppearanceSettings.Default);
        var armor = (Armor)FakeCatalog.LeatherArmor.Clone();
        player.Inventory.AddItem(armor);
        player.EquipItem(armor);
        player.LevelUp();
        Assert.Equal(player.Defense + armor.BonusDefense, player.TotalDefense);
        player.UnequipArmor();
        Assert.Equal(player.Defense, player.TotalDefense);
    }
}
