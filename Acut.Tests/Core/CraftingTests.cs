using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class CraftingTests
{
    private static (WorkshopBuilding Workshop, Player Player, GameEventDispatcher Dispatcher) CreateSetup()
    {
        var dispatcher = new GameEventDispatcher();
        var player = new Player("Тестовий алхімік", AppearanceSettings.Default);
        dispatcher.Attach(player);

        var workshop = new WorkshopBuilding(FakeCatalog.Workshop, dispatcher);
        workshop.Build();
        return (workshop, player, dispatcher);
    }

    [Fact]
    public void Craft_WithRequiredIngredients_ConsumesIngredientsCreatesPotionAndPublishesEvent()
    {
        var (workshop, player, dispatcher) = CreateSetup();
        var recipe = FakeCatalog.LargeHealRecipe;
        var smallPotion = FakeCatalog.SmallHealPotion;

        // Додаємо 2 малих зілля (потрібно для рецепта)
        player.Inventory.AddItem(smallPotion, 2);
        Assert.Equal(2, player.Inventory.GetQuantity(smallPotion));
        Assert.True(recipe.CanCraft(player.Inventory));

        var eventFired = false;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.PotionCrafted)
                eventFired = true;
        });

        var crafted = workshop.Craft(recipe, player.Inventory);

        Assert.NotNull(crafted);
        Assert.Equal(recipe.Result.Id, crafted.Id);
        Assert.Equal(0, player.Inventory.GetQuantity(smallPotion));
        Assert.Equal(1, player.Inventory.GetQuantity(recipe.Result));
        Assert.True(eventFired);
        Assert.Equal(1, player.Statistics.PotionsCrafted);
    }

    [Fact]
    public void Craft_WithoutIngredients_NothingConsumedAndReturnsNull()
    {
        var (workshop, player, dispatcher) = CreateSetup();
        var recipe = FakeCatalog.LargeHealRecipe;

        Assert.False(recipe.CanCraft(player.Inventory));

        var eventFired = false;
        dispatcher.Subscribe(e =>
        {
            if (e.Type == GameEventType.PotionCrafted)
                eventFired = true;
        });

        var result = workshop.Craft(recipe, player.Inventory);

        Assert.Null(result);
        Assert.Empty(player.Inventory.GetItems());
        Assert.False(eventFired);
        Assert.Equal(0, player.Statistics.PotionsCrafted);
    }

    [Fact]
    public void Craft_WithPartialIngredients_NothingConsumedAndReturnsNull()
    {
        var (workshop, player, _) = CreateSetup();
        var recipe = FakeCatalog.LargeHealRecipe;
        var smallPotion = FakeCatalog.SmallHealPotion;

        // Потрібно 2, але додаємо тільки 1
        player.Inventory.AddItem(smallPotion, 1);
        Assert.False(recipe.CanCraft(player.Inventory));

        var result = workshop.Craft(recipe, player.Inventory);

        Assert.Null(result);
        // Інгредієнт не списався
        Assert.Equal(1, player.Inventory.GetQuantity(smallPotion));
        Assert.Equal(0, player.Statistics.PotionsCrafted);
    }

    [Fact]
    public void Craft_InventoryFull_IngredientsFreeSlot_CraftSucceeds()
    {
        var (workshop, player, _) = CreateSetup();
        var recipe = FakeCatalog.LargeHealRecipe;
        var smallPotion = FakeCatalog.SmallHealPotion;

        // Додаємо рівно 2 малі зілля (інгредієнт)
        player.Inventory.AddItem(smallPotion, 2);

        // Заповнюємо решту інвентаря іншими предметами
        for (int i = 100; player.Inventory.GetItems().Count < player.Inventory.Capacity; i++)
        {
            player.Inventory.AddItem(new Weapon(i, $"Предмет {i}", "", 10, 1));
        }

        Assert.Equal(player.Inventory.Capacity, player.Inventory.GetItems().Count);

        var crafted = workshop.Craft(recipe, player.Inventory);

        Assert.NotNull(crafted);
        Assert.Equal(1, player.Inventory.GetQuantity(recipe.Result));
        Assert.Equal(0, player.Inventory.GetQuantity(smallPotion));
    }

    [Fact]
    public void Craft_InventoryFull_CannotFreeSlot_RejectedNothingConsumed()
    {
        var (workshop, player, _) = CreateSetup();
        var recipe = FakeCatalog.LargeHealRecipe;
        var smallPotion = FakeCatalog.SmallHealPotion;

        // Додаємо 3 малі зілля. Після витрати 2 залишиться 1, тобто слот не звільниться
        player.Inventory.AddItem(smallPotion, 3);

        // Заповнюємо решту інвентаря іншими предметами
        for (int i = 100; player.Inventory.GetItems().Count < player.Inventory.Capacity; i++)
        {
            player.Inventory.AddItem(new Weapon(i, $"Предмет {i}", "", 10, 1));
        }

        Assert.Equal(player.Inventory.Capacity, player.Inventory.GetItems().Count);

        var result = workshop.Craft(recipe, player.Inventory);

        Assert.Null(result);
        Assert.Equal(3, player.Inventory.GetQuantity(smallPotion));
    }
}
