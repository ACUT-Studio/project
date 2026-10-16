namespace Acut.Core.Domain.Items;

public sealed class Recipe
{
    public Recipe(int id, string name, Potion result, IReadOnlyDictionary<Item, int> ingredients)
    {
        Id = id;
        Name = name;
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
    }

    public int Id { get; }
    public string Name { get; }
    public Potion Result { get; }
    public IReadOnlyDictionary<Item, int> Ingredients { get; }

    public bool CanCraft(Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return Ingredients.All(pair => inventory.HasItem(pair.Key, pair.Value));
    }
}
