using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;

namespace Acut.Core.Domain.Economy;

public sealed class WorkshopBuilding : Building
{
    public WorkshopBuilding(BuildingType type, GameEventDispatcher? dispatcher = null) : base(type)
    {
        Dispatcher = dispatcher;
    }

    public override BuildingKind Kind => BuildingKind.Workshop;

    /// <summary>Диспетчер подій для публікації PotionCrafted.</summary>
    public GameEventDispatcher? Dispatcher { get; set; }

    /// <summary>
    /// Крафт зілля за рецептом: перевіряє CanCraft, списує інгредієнти з інвентаря,
    /// додає результат і публікує подію PotionCrafted.
    /// Якщо інгредієнтів недостатньо або немає місця — нічого не списує і повертає null.
    /// </summary>
    public Item? Craft(Recipe recipe, Inventory inventory, GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(inventory);

        if (!recipe.CanCraft(inventory))
            return null;

        var willFreeSlot = recipe.Ingredients.Any(i => inventory.GetQuantity(i.Key) == i.Value);
        if (!inventory.CanAddItem(recipe.Result) && !willFreeSlot)
            return null;

        foreach (var (ingredient, qty) in recipe.Ingredients)
        {
            inventory.RemoveItem(ingredient, qty);
        }

        var result = (Potion)recipe.Result.Clone();
        inventory.AddItem(result, 1);

        var d = dispatcher ?? Dispatcher;
        d?.Publish(new GameEvent(GameEventType.PotionCrafted, 1));

        return result;
    }
}
