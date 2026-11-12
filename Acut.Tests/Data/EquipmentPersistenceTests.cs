using Acut.Core.Domain.Items;
using Acut.Core.Services;
using Acut.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Acut.Tests.Data;

public sealed class EquipmentPersistenceTests
{
    [Fact]
    public void SaveLoad_PreservesBothEquipmentSlotsAndBaseStatsWithoutDoublingBonuses()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        var weapon = db.Catalog.GetItems().OfType<Weapon>().First();
        var armor = db.Catalog.GetItems().OfType<Armor>().First();
        session.Player.Inventory.AddItem(weapon, 2);
        session.Player.Inventory.AddItem(armor);
        session.Player.EquipItem(weapon);
        session.Player.EquipItem(armor);
        session.ChangeState(GameState.Paused);
        db.Saves.Save(session);

        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(GameState.Paused, loaded.CurrentState);
        Assert.Equal(weapon.Id, loaded.Player.EquippedWeapon!.Id);
        Assert.Equal(armor.Id, loaded.Player.EquippedArmor!.Id);
        Assert.Equal(session.Player.AttackPower, loaded.Player.AttackPower);
        Assert.Equal(session.Player.TotalAttackPower, loaded.Player.TotalAttackPower);
        Assert.Equal(session.Player.TotalDefense, loaded.Player.TotalDefense);
        Assert.Equal(2, loaded.Player.Inventory.GetQuantity(weapon));
        db.Saves.Save(loaded);
        var reloaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(session.Player.TotalAttackPower, reloaded.Player.TotalAttackPower);
        reloaded.Player.UnequipWeapon();
        reloaded.Player.UnequipArmor();
        db.Saves.Save(reloaded);
        var unequipped = db.Saves.Load(session.SessionId)!;
        Assert.Null(unequipped.Player.EquippedWeapon);
        Assert.Null(unequipped.Player.EquippedArmor);
        Assert.Equal(2, unequipped.Player.Inventory.GetItems().Count);
    }

    [Fact]
    public void FailedSave_RollsBackEquipmentAndInventoryTogether()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        var weapon = db.Catalog.GetItems().OfType<Weapon>().First();
        session.Player.Inventory.AddItem(weapon);
        session.Player.EquipItem(weapon);
        db.Saves.Save(session);
        session.Player.UnequipWeapon();
        session.Player.Inventory.AddItem(new Weapon(99999, "Невідомий меч", "", 1, 1));

        Assert.Throws<SqliteException>(() => db.Saves.Save(session));

        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(weapon.Id, loaded.Player.EquippedWeapon!.Id);
        Assert.Single(loaded.Player.Inventory.GetItems());
    }

    [Fact]
    public void VersionTwoMigration_PreservesInventoryAndStartsWithEmptyEquipmentSlots()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        var weapon = db.Catalog.GetItems().OfType<Weapon>().First();
        session.Player.Inventory.AddItem(weapon, 3);
        db.Saves.Save(session);
        using (var connection = db.Factory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                DROP INDEX IX_InventoryItem_EquipmentSlot;
                ALTER TABLE InventoryItem DROP COLUMN EquipmentSlot;
                ALTER TABLE ActiveBattle DROP COLUMN PlayerDefending;
                ALTER TABLE ActiveBattle DROP COLUMN EnemyDefending;
                PRAGMA user_version = 2;
                """;
            command.ExecuteNonQuery();
        }
        var initializer = new DatabaseInitializer(db.Factory);
        initializer.Initialize();
        initializer.Initialize();
        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(3, loaded.Player.Inventory.GetQuantity(weapon));
        Assert.Null(loaded.Player.EquippedWeapon);
        Assert.Null(loaded.Player.EquippedArmor);
        loaded.Player.EquipItem(loaded.Player.Inventory.GetItems().Single().Item);
        db.Saves.Save(loaded);
        Assert.Equal(weapon.Id, db.Saves.Load(session.SessionId)!.Player.EquippedWeapon!.Id);
    }

    [Fact]
    public void EquipmentSlotConstraint_RejectsInvalidNamesAndDuplicateSlots()
    {
        using var db = new TestDatabase();
        var session = GameSession.StartNewGame("Тест", null, db.Catalog);
        foreach (var weapon in db.Catalog.GetItems().OfType<Weapon>()) session.Player.Inventory.AddItem(weapon);
        db.Saves.Save(session);
        using var connection = db.Factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE InventoryItem SET EquipmentSlot = 'Invalid'";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = "UPDATE InventoryItem SET EquipmentSlot = 'Weapon'";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }
}
