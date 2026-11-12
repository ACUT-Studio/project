-- Спорядження є посиланням на власний предмет інвентаря; старі збереження починають без нього.
ALTER TABLE InventoryItem ADD COLUMN EquipmentSlot TEXT
    CHECK (EquipmentSlot IS NULL OR EquipmentSlot IN ('Weapon', 'Armor'));
CREATE UNIQUE INDEX IX_InventoryItem_EquipmentSlot
    ON InventoryItem (PlayerId, EquipmentSlot) WHERE EquipmentSlot IS NOT NULL;
