namespace Acut.Core.Domain.Items;

public sealed class ItemStack
{
    internal ItemStack(Item item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }

    public Item Item { get; }
    public int Quantity { get; internal set; }
}
