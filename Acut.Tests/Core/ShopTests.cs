using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class ShopTests
{
    private static (Shop Shop, Player Player, GameEventDispatcher Dispatcher) CreateSetup(int gold = 500)
    {
        var catalog = new FakeCatalog();
        var dispatcher = new GameEventDispatcher();
        var player = new Player("Тестовий гравець", AppearanceSettings.Default, gold: gold);
        dispatcher.Attach(player);
        var shop = new Shop(catalog, dispatcher);
        return (shop, player, dispatcher);
    }

    [Fact]
    public void BuyItem_SufficientGoldAndSpace_DeductsGoldAddsToInventoryAndPublishesEvent()
    {
        var (shop, player, dispatcher) = CreateSetup(gold: 300);
        var sword = FakeCatalog.IronSword;
        var eventFired = false;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.ItemBought)
                eventFired = true;
        });

        var result = shop.BuyItem(sword, player);

        Assert.True(result);
        Assert.Equal(150, player.Gold); // 300 - 150 = 150
        Assert.True(player.Inventory.HasItem(sword));
        Assert.Equal(1, player.Inventory.GetQuantity(sword));
        Assert.True(eventFired);
    }

    [Fact]
    public void BuyItem_InsufficientGold_RejectedWithoutSideEffects()
    {
        var (shop, player, dispatcher) = CreateSetup(gold: 50);
        var sword = FakeCatalog.IronSword;
        var eventFired = false;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.ItemBought)
                eventFired = true;
        });

        var result = shop.BuyItem(sword, player);

        Assert.False(result);
        Assert.Equal(50, player.Gold);
        Assert.False(player.Inventory.HasItem(sword));
        Assert.Empty(player.Inventory.GetItems());
        Assert.False(eventFired);
    }

    [Fact]
    public void BuyItem_InventoryFullForNewItem_RejectedWithoutSideEffects()
    {
        var (shop, player, dispatcher) = CreateSetup(gold: 1000);

        // Заповнюємо інвентар до повної місткості різними предметами
        for (int i = 100; player.Inventory.GetItems().Count < player.Inventory.Capacity; i++)
        {
            player.Inventory.AddItem(new Weapon(i, $"Предмет {i}", "", 10, 1));
        }

        var sword = FakeCatalog.IronSword;
        var eventFired = false;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.ItemBought)
                eventFired = true;
        });

        // Спроба купити інший предмет (меч), коли всі слоти зайняті
        var result = shop.BuyItem(sword, player);

        Assert.False(result);
        Assert.Equal(1000, player.Gold);
        Assert.False(player.Inventory.HasItem(sword));
        Assert.False(eventFired);
    }

    [Fact]
    public void BuyItem_InventoryFullExistingStack_SuccessfullyPurchased()
    {
        var (shop, player, _) = CreateSetup(gold: 1000);
        var smallPotion = FakeCatalog.SmallHealPotion;

        // Додаємо зілля першим
        player.Inventory.AddItem(smallPotion, 2);

        // Заповнюємо решту слотів до місткості
        for (int i = 100; player.Inventory.GetItems().Count < player.Inventory.Capacity; i++)
        {
            player.Inventory.AddItem(new Weapon(i, $"Предмет {i}", "", 10, 1));
        }

        Assert.Equal(player.Inventory.Capacity, player.Inventory.GetItems().Count);

        // Купівля того самого зілля збільшує стак
        var result = shop.BuyItem(smallPotion, player);

        Assert.True(result);
        Assert.Equal(1000 - smallPotion.Price, player.Gold);
        Assert.Equal(3, player.Inventory.GetQuantity(smallPotion));
    }

    [Fact]
    public void BuyItem_DoesNotMutateShopAssortmentOnPlayerUpgrade()
    {
        var (shop, player, _) = CreateSetup(gold: 1000);
        var sword = FakeCatalog.IronSword;

        Assert.True(shop.BuyItem(sword, player));

        var playerSword = player.Inventory.GetItems().First(s => s.Item.Id == sword.Id).Item;
        Assert.Equal(1, playerSword.Level);

        // Покращуємо куплений меч
        Assert.True(shop.UpgradeItem(playerSword, player));
        Assert.Equal(2, playerSword.Level);

        // Предмет в асортименті магазину залишився рівня 1
        var shopSword = shop.Assortment.First(i => i.Id == sword.Id);
        Assert.Equal(1, shopSword.Level);
    }

    [Fact]
    public void UpgradeItem_Success_DeductsGoldUpgradesLevelAndScalesStats()
    {
        var (shop, player, _) = CreateSetup(gold: 500);
        var sword = (Weapon)FakeCatalog.IronSword.Clone();
        player.Inventory.AddItem(sword);

        // Level 1: BonusAttack = 5, Price = 150, UpgradeCost = 150
        Assert.Equal(1, sword.Level);
        Assert.Equal(5, sword.BonusAttack);
        Assert.Equal(150, sword.UpgradeCost);

        var upgraded = shop.UpgradeItem(sword, player);

        Assert.True(upgraded);
        Assert.Equal(350, player.Gold); // 500 - 150 = 350
        Assert.Equal(2, sword.Level);
        Assert.Equal(10, sword.BonusAttack); // 5 * 2 = 10
    }

    [Fact]
    public void UpgradeItem_InsufficientGold_RejectedWithoutSideEffects()
    {
        var (shop, player, _) = CreateSetup(gold: 100);
        var sword = (Weapon)FakeCatalog.IronSword.Clone();
        player.Inventory.AddItem(sword);

        // UpgradeCost = 150, у гравця 100
        var result = shop.UpgradeItem(sword, player);

        Assert.False(result);
        Assert.Equal(100, player.Gold);
        Assert.Equal(1, sword.Level);
        Assert.Equal(5, sword.BonusAttack);
    }

    [Fact]
    public void UpgradeItem_ItemNotInInventory_Rejected()
    {
        var (shop, player, _) = CreateSetup(gold: 500);
        var sword = FakeCatalog.IronSword;

        var result = shop.UpgradeItem(sword, player);

        Assert.False(result);
        Assert.Equal(500, player.Gold);
    }

    [Fact]
    public void UpgradeItem_MaxLevelReached_Rejected()
    {
        var (shop, player, _) = CreateSetup(gold: 5000);
        var armor = (Armor)FakeCatalog.LeatherArmor.Clone();
        player.Inventory.AddItem(armor);

        // Підвищуємо до MaxItemLevel (5)
        for (int i = 1; i < GameConfig.MaxItemLevel; i++)
        {
            Assert.True(shop.UpgradeItem(armor, player));
        }

        Assert.Equal(GameConfig.MaxItemLevel, armor.Level);
        Assert.False(armor.CanUpgrade);

        var goldBeforeAttempt = player.Gold;
        var result = shop.UpgradeItem(armor, player);

        Assert.False(result);
        Assert.Equal(goldBeforeAttempt, player.Gold);
        Assert.Equal(GameConfig.MaxItemLevel, armor.Level);
    }
}
