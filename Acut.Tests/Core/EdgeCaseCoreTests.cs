using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class EdgeCaseCoreTests
{
    [Theory]
    [InlineData("А", true)]
    [InlineData("123456789012345678901234", true)] // Рівно 24 символи (MaxPlayerNameLength)
    [InlineData("   123456789012345678901234   ", true)] // 30 символів, але після Trim() рівно 24
    [InlineData("1234567890123456789012345", false)] // 25 символів — перевищення ліміту
    [InlineData(" \t \r \n ", false)] // Лише пробільні символи
    public void GetPlayerNameError_BoundaryLengthsAndWhitespaceTrimming_ValidatesCorrectly(string input, bool isValid)
    {
        var error = GameSession.GetPlayerNameError(input);

        if (isValid) Assert.Null(error);
        else Assert.NotNull(error);
    }

    [Fact]
    public void Character_Constructor_ClampsNegativeAndExcessiveInitialHealth()
    {
        var negativeHealthPlayer = new Player("Герой", AppearanceSettings.Default, health: -50, maxHealth: 100);
        var excessiveHealthPlayer = new Player("Герой", AppearanceSettings.Default, health: 500, maxHealth: 100);

        Assert.Equal(0, negativeHealthPlayer.Health);
        Assert.False(negativeHealthPlayer.IsAlive);
        Assert.Equal(100, excessiveHealthPlayer.Health);
        Assert.True(excessiveHealthPlayer.IsAlive);
    }

    [Fact]
    public void Player_GainExperience_MultipleLevelsAtOnce_ScalesThresholdsAndRestoresFullHealth()
    {
        var player = new Player("Герой", AppearanceSettings.Default, health: 10, maxHealth: 100);

        // Рівень 1 -> 2 вимагає 100 XP, Рівень 2 -> 3 вимагає 200 XP (разом 300 XP)
        player.GainExperience(350);

        Assert.Equal(3, player.Level);
        Assert.Equal(50, player.Experience);
        Assert.Equal(300, player.ExperienceForNextLevel);
        Assert.Equal(2, player.SkillPoints);
        Assert.Equal(100 + GameConfig.HealthPerLevel * 2, player.MaxHealth);
        Assert.Equal(player.MaxHealth, player.Health); // LevelUp повністю відновлює здоров'я
    }

    [Fact]
    public void ResourceAmounts_CheckedOverflowAndOddNumberHalf_BehavesCorrectly()
    {
        var max = new ResourceAmounts(int.MaxValue, 10, 10);
        Assert.Throws<OverflowException>(() => max.Add(new ResourceAmounts(1, 0, 0)));
        Assert.Throws<OverflowException>(() => new ResourceAmounts(int.MaxValue / 2 + 2).Multiply(2));

        // Непарна кількість ресурсів при діленні навпіл округлюється вниз до 0
        var oddUnit = new ResourceAmounts(1, 1, 1);
        var refund = oddUnit.Half();
        Assert.True(refund.IsEmpty);
        Assert.Equal(new ResourceAmounts(0, 0, 0), refund);
    }

    [Fact]
    public void StockMarket_PennyStockBoundary_BuyChargesMinimumOneGoldWhileSellYieldsZeroGold()
    {
        var pennyStock = new Stock(1, "PENNY", "Копійчана акція", basePrice: 0.01m, volatility: 0m);
        var market = new StockMarket([pennyStock]);
        var player = new Player("Інвестор", AppearanceSettings.Default, gold: 10);

        // Купівля 1 акції за 0.01m округлюється до 0, але мінімальна ціна угоди = 1 золота
        Assert.True(market.Buy(pennyStock, 1, player));
        Assert.Equal(9, player.Gold);
        Assert.Equal(1, player.StockHoldings[pennyStock.Id]);

        // Продаж 1 акції за 0.01m дає Math.Round(0.01m) == 0 золота, але списує акцію
        Assert.True(market.Sell(pennyStock, 1, player));
        Assert.Equal(9, player.Gold);
        Assert.False(player.StockHoldings.ContainsKey(pennyStock.Id));
    }

  
    [Fact]
    public void Shop_BuyItemWithForgedPrice_DeductsAssortmentPriceAndClonesAssortmentStats()
    {
        var shop = new Shop(new FakeCatalog());
        var player = new Player("Гравець", AppearanceSettings.Default, gold: 200);
        var forgedSword = new Weapon(FakeCatalog.IronSword.Id, "Підроблений меч", "", price: 1, bonusAttack: 999);

        Assert.True(shop.BuyItem(forgedSword, player));
        Assert.Equal(200 - FakeCatalog.IronSword.Price, player.Gold);

        var bought = (Weapon)player.Inventory.GetItems().Single().Item;
        Assert.Equal(FakeCatalog.IronSword.BonusAttack, bought.BonusAttack);
    }

    [Theory]
    [InlineData(33, 99, BattleAction.Defend)] // 33 * 3 == 99 <= 99 (точна межа)
    [InlineData(34, 99, BattleAction.Attack)] // 34 * 3 == 102 > 99
    public void BalancedStrategy_ExactOneThirdHealthBoundary_ChoosesExpectedAction(int health, int maxHealth, BattleAction expected)
    {
        var strategy = new BalancedStrategy();
        var enemy = new Enemy("Вовк", EnemyType.Wolf, maxHealth, 10, 2, 10, 10, strategy, health);
        var player = new Player("Герой", AppearanceSettings.Default);

        Assert.Equal(expected, strategy.ChooseAction(enemy, player));
    }

    [Theory]
    [InlineData(25, 100, 100, 100, BattleAction.Defend)] // Ворог має рівно 25% HP -> Defend
    [InlineData(26, 100, 100, 100, BattleAction.Attack)] // Ворог > 25% HP і гравець > 50% HP -> Attack
    [InlineData(100, 100, 50, 100, BattleAction.Defend)] // Ворог здоровий, але гравець має <= 50% HP -> Defend
    [InlineData(100, 100, 51, 100, BattleAction.Attack)] // Гравець має 51% HP -> Attack
    public void DefensiveStrategy_SelfQuarterAndTargetHalfBoundaries_ChoosesExpectedAction(
        int enemyHp, int enemyMaxHp, int playerHp, int playerMaxHp, BattleAction expected)
    {
        var strategy = new DefensiveStrategy();
        var enemy = new Enemy("Бандит", EnemyType.Bandit, enemyMaxHp, 10, 2, 10, 10, strategy, enemyHp);
        var player = new Player("Герой", AppearanceSettings.Default, health: playerHp, maxHealth: playerMaxHp);

        Assert.Equal(expected, strategy.ChooseAction(enemy, player));
    }

    [Fact]
    public void Battle_ZeroEffectPotionAndOverkillDamage_AreHandledAtBoundaries()
    {
        var player = new Player("Герой", AppearanceSettings.Default, health: 50, maxHealth: 100, attackPower: 500);
        var zeroPotion = new Potion(99, "Пустушка", "", 10, PotionEffect.Heal, effectValue: 0);
        player.Inventory.AddItem(zeroPotion);

        var enemy = new Enemy("Гоблін", EnemyType.Goblin, maxHealth: 12, attackPower: 5, defense: 0, 10, 10, new AggressiveStrategy());
        var battle = new Battle(player, enemy);
        battle.Start();

        // Зілля з EffectValue == 0 не дозволяється використовувати в бою
        Assert.False(battle.CanExecutePlayerAction(BattleAction.UseItem, zeroPotion));
        Assert.Throws<GameRuleException>(() => battle.ExecutePlayerAction(BattleAction.UseItem, zeroPotion));

        // Атака силою 500 по ворогу з 12 HP наносить рівно 12 шкоди (без overkill у ActionResult)
        var result = battle.ExecutePlayerAction(BattleAction.Attack);
        Assert.Equal(12, result.Damage);
        Assert.Equal(0, enemy.Health);
    }

    [Fact]
    public void BuildingAndQuest_RestoreWithOutOfBoundsValues_ClampsToValidRanges()
    {
        var building = BuildingFactory.Create(FakeCatalog.Hut); // MaxLevel = 3
        Assert.Throws<ArgumentOutOfRangeException>(() => building.Restore(Guid.NewGuid(), 0, BuildingStatus.Active));

        building.Restore(Guid.NewGuid(), level: 99, BuildingStatus.MaxLevel);
        Assert.Equal(FakeCatalog.Hut.MaxLevel, building.Level);

        var quest = new Quest(1, "Квест", "", GameEventType.BuildingBuilt, target: 5, 10, 10);
        quest.Restore(QuestStatus.InProgress, progress: -10);
        Assert.Equal(0, quest.Progress);

        quest.Restore(QuestStatus.Completed, progress: 999);
        Assert.Equal(5, quest.Progress);
    }

    [Fact]
    public void Building_UnderConstructionOrUnknownResource_ProducesZeroResourcesAndZeroPopulation()
    {
        var player = new Player("Мер", AppearanceSettings.Default);
        var dispatcher = new GameEventDispatcher();
        var city = new City("Місто", player, dispatcher);

        // Будівля у статусі UnderConstruction (не викликано Build)
        var unbuiltHut = BuildingFactory.Create(FakeCatalog.Hut);
        city.RestoreBuilding(unbuiltHut);

        // Виробнича будівля з невідомим типом ресурсу
        var unknownProdType = new BuildingType(77, BuildingKind.Production, "Кар'єр", 0, 0, 1, 0, 0, "Mythril", 10, null);
        var unknownProd = BuildingFactory.Create(unknownProdType);
        unknownProd.Build();
        city.RestoreBuilding(unknownProd);

        Assert.Equal(0, city.Population);
        Assert.True(city.CollectAllResources().IsEmpty);
        Assert.Throws<InvalidOperationException>(() => unknownProd.Build()); // Повторний Build заборонений
    }
}