using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;

namespace Acut.Core.Services;

/// <summary>Магазин: асортимент береться з каталогу ItemDefinition.</summary>
public sealed class Shop
{
    public Shop(ICatalogRepository catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Assortment = catalog.GetItems().ToList();
    }

    public List<Item> Assortment { get; }

    public bool BuyItem(Item item, Player player)
    {
        // TODO(Етап 6): SpendGold(item.Price), Inventory.AddItem, подія ItemBought.
        throw new NotImplementedException("Етап 6: Shop.BuyItem");
    }

    public bool UpgradeItem(Item item, Player player)
    {
        // TODO(Етап 6): вартість покращення, Item.Upgrade(), оновлення інвентаря.
        throw new NotImplementedException("Етап 6: Shop.UpgradeItem");
    }
}
