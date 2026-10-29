using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;

namespace Acut.Core.Services;

/// <summary>Магазин: асортимент береться з каталогу ItemDefinition.</summary>
public sealed class Shop
{
    public Shop(ICatalogRepository catalog, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Assortment = catalog.GetItems().ToList();
        Dispatcher = dispatcher;
    }

    public List<Item> Assortment { get; }
    public GameEventDispatcher? Dispatcher { get; set; }

    /// <summary>
    /// Купує предмет: списує золото, додає предмет в інвентар гравця і публікує подію ItemBought.
    /// Відхиляється без побічних ефектів, якщо не вистачає золота або немає місця в інвентарі.
    /// </summary>
    public bool BuyItem(Item item, Player player, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(player);

        var shopItem = Assortment.FirstOrDefault(i => i.Id == item.Id) ?? item;
        if (!player.Inventory.CanAddItem(shopItem))
            return false;

        if (player.Gold < shopItem.Price)
            return false;

        if (!player.SpendGold(shopItem.Price))
            return false;

        if (!player.Inventory.AddItem(shopItem.Clone()))
        {
            player.AddGold(shopItem.Price);
            return false;
        }

        var d = dispatcher ?? Dispatcher;
        d?.Publish(new GameEvent(GameEventType.ItemBought, 1));
        return true;
    }

    /// <summary>
    /// Покращує предмет гравця в інвентарі за плату (UpgradeCost).
    /// Відхиляється, якщо предмета немає, він уже на MaxLevel або не вистачає золота.
    /// </summary>
    public bool UpgradeItem(Item item, Player player)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(player);

        var stack = player.Inventory.GetItems().FirstOrDefault(s => s.Item.Id == item.Id);
        if (stack is null)
            return false;

        var target = stack.Item;
        if (!target.CanUpgrade)
            return false;

        var cost = target.UpgradeCost;
        if (player.Gold < cost)
            return false;

        if (!player.SpendGold(cost))
            return false;

        if (!target.Upgrade())
        {
            player.AddGold(cost);
            return false;
        }

        return true;
    }
}
