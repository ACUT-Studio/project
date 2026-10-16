namespace Acut.Core.Domain.Items;

/// <summary>Інвентар гравця: один стак на кожен предмет каталогу (як у таблиці InventoryItem).</summary>
public sealed class Inventory
{
    private readonly Dictionary<int, ItemStack> _stacks = new();

    public Inventory(int capacity = GameConfig.InventoryCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>Максимальна кількість різних стаків.</summary>
    public int Capacity { get; }

    public bool AddItem(Item item, int qty = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfLessThan(qty, 1);

        if (_stacks.TryGetValue(item.Id, out var stack))
        {
            stack.Quantity += qty;
            return true;
        }
        if (_stacks.Count >= Capacity) return false;

        _stacks[item.Id] = new ItemStack(item, qty);
        return true;
    }

    public bool RemoveItem(Item item, int qty = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfLessThan(qty, 1);

        if (!_stacks.TryGetValue(item.Id, out var stack) || stack.Quantity < qty) return false;

        stack.Quantity -= qty;
        if (stack.Quantity == 0) _stacks.Remove(item.Id);
        return true;
    }

    public bool HasItem(Item item, int qty = 1) => GetQuantity(item) >= qty;

    public int GetQuantity(Item item) =>
        _stacks.TryGetValue(item.Id, out var stack) ? stack.Quantity : 0;

    public List<ItemStack> GetItems() => _stacks.Values.ToList();
}
