using Acut.Core.Domain.Items;
using Acut.Core.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class EdgeCaseDataTests
{
    [Fact]
    public void Load_WhenPlayerRecordIsMissing_ThrowsInvalidDataException()
    {
        using var db = new TestDatabase();
        var saveId = Guid.NewGuid();

        db.Execute($"""
            INSERT INTO GameSave (SaveId, Name, CreatedAt, UpdatedAt, CurrentState)
            VALUES ('{saveId}', 'Пошкоджене збереження', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z', 'City');
            """);

        var ex = Assert.Throws<InvalidDataException>(() => db.Saves.Load(saveId));
        Assert.Contains("відсутній запис гравця", ex.Message);
    }

    [Fact]
    public void Load_WhenCityRecordIsMissing_ThrowsInvalidDataException()
    {
        using var db = new TestDatabase();
        var saveId = Guid.NewGuid();

        db.Execute($"""
            INSERT INTO GameSave (SaveId, Name, CreatedAt, UpdatedAt, CurrentState)
            VALUES ('{saveId}', 'Без міста', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z', 'City');

            INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower, Defense, SkillPoints, HairStyle, SkinColor, Outfit, Wood, Stone)
            VALUES ('{saveId}', 'Герой', 1, 0, 100, 100, 100, 10, 2, 1, 'Short', 'Light', 'Traveler', 150, 0);
            """);

        var ex = Assert.Throws<InvalidDataException>(() => db.Saves.Load(saveId));
        Assert.Contains("відсутній запис міста", ex.Message);
    }

    [Fact]
    public void Load_WhenEquipmentSlotDoesNotMatchItemKind_ThrowsInvalidDataException()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Герой", null, db.Catalog);
        var armor = db.Catalog.GetItems().OfType<Armor>().First();
        session.Player.Inventory.AddItem(armor, 1);
        db.Saves.Save(session);

        // Записуємо броню у слот 'Weapon' напряму в таблицю
        db.Execute($"UPDATE InventoryItem SET EquipmentSlot = 'Weapon' WHERE ItemId = {armor.Id};");

        var ex = Assert.Throws<InvalidDataException>(() => db.Saves.Load(session.SessionId));
        Assert.Contains("тип спорядження не відповідає слоту", ex.Message);
    }

    [Fact]
    public void Save_WithZeroOrNegativeStockHoldings_FiltersThemOutAndPreservesSkillUnlockTimestamp()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Інвестор", null, db.Catalog);

        // Додаємо 0 акцій у словник StockHoldings (в БД CHECK (Quantity >= 1))
        session.Player.StockHoldings[1] = 0;

        var skill = session.Player.SkillTree.Skills.First(s => s.Id == 1);
        session.Player.SkillTree.Unlock(skill, session.Dispatcher);
        db.Saves.Save(session);

        var firstUnlockedAt = (string)db.Scalar("SELECT UnlockedAt FROM PlayerSkill LIMIT 1;")!;

        // Повторне збереження не перезаписує час вивчення навички завдяки INSERT OR IGNORE
        db.Saves.Save(session);
        var secondUnlockedAt = (string)db.Scalar("SELECT UnlockedAt FROM PlayerSkill LIMIT 1;")!;

        Assert.Equal(firstUnlockedAt, secondUnlockedAt);
        Assert.Equal(0, db.Count("PlayerStock"));
    }

    [Fact]
    public void DatabaseTriggersAndCheckConstraints_RejectInvalidBuildingResourceAndDuplicateEquipmentSlots()
    {
        using var db = new TestDatabase();

        // Тригер BuildingType_Resource_Insert блокує невідомий ресурс
        Assert.Throws<SqliteException>(() => db.Execute("""
            INSERT INTO BuildingType (BuildingTypeId, Kind, Name, BuildCost, UpgradeCost, MaxLevel, IncomePerTick, Resource, ProductionRate)
            VALUES (99, 'Production', 'Алхімічна вежа', 10, 10, 1, 0, 'Mana', 5);
            """));

        // Унікальний індекс IX_InventoryItem_EquipmentSlot блокує два предмети в одному слоті
        var session = GameSession.StartNewGame("Герой", null, db.Catalog);
        var weapons = db.Catalog.GetItems().OfType<Weapon>().ToList();
        session.Player.Inventory.AddItem(weapons[0], 1);
        session.Player.Inventory.AddItem(weapons[1], 1);
        session.Player.EquipItem(weapons[0]);
        db.Saves.Save(session);

        Assert.Throws<SqliteException>(() =>
            db.Execute($"UPDATE InventoryItem SET EquipmentSlot = 'Weapon' WHERE ItemId = {weapons[1].Id};"));
    }
}