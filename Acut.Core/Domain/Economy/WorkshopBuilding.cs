using Acut.Core.Domain.Items;

namespace Acut.Core.Domain.Economy;

public sealed class WorkshopBuilding : Building
{
    public WorkshopBuilding(BuildingType type) : base(type) { }

    public override BuildingKind Kind => BuildingKind.Workshop;

    /// <summary>Крафт зілля за рецептом: списує інгредієнти з інвентаря й додає результат.</summary>
    public Item Craft(Recipe recipe, Inventory inventory)
    {
        // TODO(Етап 6): перевірка CanCraft, списання інгредієнтів, подія PotionCrafted.
        throw new NotImplementedException("Етап 6: WorkshopBuilding.Craft");
    }
}
