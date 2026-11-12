-- Версія 2: три ресурси, складові цін і фактичні витрати на будівлі.
ALTER TABLE Player ADD COLUMN Wood INTEGER NOT NULL DEFAULT 150 CHECK (Wood >= 0);
ALTER TABLE Player ADD COLUMN Stone INTEGER NOT NULL DEFAULT 0 CHECK (Stone >= 0);
ALTER TABLE BuildingType ADD COLUMN BuildWoodCost INTEGER NOT NULL DEFAULT 0 CHECK (BuildWoodCost >= 0);
ALTER TABLE BuildingType ADD COLUMN BuildStoneCost INTEGER NOT NULL DEFAULT 0 CHECK (BuildStoneCost >= 0);
ALTER TABLE BuildingType ADD COLUMN UpgradeWoodCost INTEGER NOT NULL DEFAULT 0 CHECK (UpgradeWoodCost >= 0);
ALTER TABLE BuildingType ADD COLUMN UpgradeStoneCost INTEGER NOT NULL DEFAULT 0 CHECK (UpgradeStoneCost >= 0);
ALTER TABLE CityBuilding ADD COLUMN SpentGold INTEGER NOT NULL DEFAULT 0 CHECK (SpentGold >= 0);
ALTER TABLE CityBuilding ADD COLUMN SpentWood INTEGER NOT NULL DEFAULT 0 CHECK (SpentWood >= 0);
ALTER TABLE CityBuilding ADD COLUMN SpentStone INTEGER NOT NULL DEFAULT 0 CHECK (SpentStone >= 0);

-- Старі будівлі були оплачені золотом; зберігаємо фактичні вкладення до зміни каталогу.
UPDATE CityBuilding SET SpentGold = (
    SELECT BuildCost + (CityBuilding.Level - 1) * UpgradeCost
    FROM BuildingType WHERE BuildingTypeId = CityBuilding.BuildingTypeId
);

-- Залишаємо одну спеціальну будівлю кожного виду, повертаючи половину витрат за зайві.
CREATE TEMP TABLE ExtraSpecialBuildings AS
SELECT BuildingId FROM (
    SELECT b.BuildingId,
           ROW_NUMBER() OVER (PARTITION BY b.CityId, t.Kind ORDER BY b.rowid) AS Number
    FROM CityBuilding b JOIN BuildingType t USING (BuildingTypeId)
    WHERE t.Kind IN ('Workshop', 'Market')
) WHERE Number > 1;
UPDATE Player SET Gold = Gold + COALESCE((
    SELECT SUM(b.SpentGold / 2) FROM CityBuilding b JOIN City c USING (CityId)
    WHERE c.SaveId = Player.SaveId AND b.BuildingId IN (SELECT BuildingId FROM ExtraSpecialBuildings)
), 0);
DELETE FROM CityBuilding WHERE BuildingId IN (SELECT BuildingId FROM ExtraSpecialBuildings);
DROP TABLE ExtraSpecialBuildings;

UPDATE BuildingType SET
    Name = CASE BuildingTypeId WHEN 2 THEN 'Кам''яна хатина' WHEN 3 THEN 'Лісопил' ELSE Name END,
    BuildCost = CASE BuildingTypeId WHEN 2 THEN 50 WHEN 5 THEN 100 WHEN 6 THEN 100 ELSE 0 END,
    BuildWoodCost = CASE BuildingTypeId WHEN 1 THEN 100 WHEN 2 THEN 100 WHEN 3 THEN 120 WHEN 4 THEN 150 WHEN 5 THEN 120 WHEN 6 THEN 150 END,
    BuildStoneCost = CASE BuildingTypeId WHEN 2 THEN 80 WHEN 5 THEN 100 WHEN 6 THEN 150 ELSE 0 END,
    UpgradeCost = CASE BuildingTypeId WHEN 2 THEN 40 ELSE 0 END,
    UpgradeWoodCost = CASE BuildingTypeId WHEN 1 THEN 80 WHEN 2 THEN 80 WHEN 3 THEN 100 WHEN 4 THEN 120 ELSE 0 END,
    UpgradeStoneCost = CASE BuildingTypeId WHEN 2 THEN 60 ELSE 0 END,
    IncomePerTick = CASE BuildingTypeId WHEN 1 THEN 2 WHEN 2 THEN 5 ELSE 0 END,
    Resource = CASE BuildingTypeId WHEN 1 THEN 'Gold' WHEN 2 THEN 'Gold' WHEN 3 THEN 'Wood' WHEN 4 THEN 'Stone' ELSE NULL END,
    ProductionRate = CASE WHEN BuildingTypeId IN (3, 4) THEN 4 ELSE NULL END,
    MaxLevel = CASE WHEN Kind IN ('Market', 'Workshop') THEN 1 ELSE MaxLevel END,
    RequiredSkillId = NULL
WHERE BuildingTypeId BETWEEN 1 AND 6;
UPDATE CityBuilding SET Level = 1, Status = 'MaxLevel'
WHERE BuildingTypeId IN (SELECT BuildingTypeId FROM BuildingType WHERE Kind IN ('Market', 'Workshop'));

CREATE TRIGGER BuildingType_Resource_Insert BEFORE INSERT ON BuildingType
WHEN NEW.Resource IS NOT NULL AND NEW.Resource NOT IN ('Gold', 'Wood', 'Stone')
BEGIN SELECT RAISE(ABORT, 'Невідомий ресурс'); END;
CREATE TRIGGER BuildingType_Resource_Update BEFORE UPDATE OF Resource ON BuildingType
WHEN NEW.Resource IS NOT NULL AND NEW.Resource NOT IN ('Gold', 'Wood', 'Stone')
BEGIN SELECT RAISE(ABORT, 'Невідомий ресурс'); END;

CREATE TABLE EconomySettings (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    StartingGold INTEGER NOT NULL CHECK (StartingGold >= 0),
    StartingWood INTEGER NOT NULL CHECK (StartingWood >= 0),
    StartingStone INTEGER NOT NULL CHECK (StartingStone >= 0),
    StartingBuildingTypeId INTEGER REFERENCES BuildingType (BuildingTypeId)
);
INSERT INTO EconomySettings SELECT 1, 0, 150, 0, 3 WHERE EXISTS (SELECT 1 FROM BuildingType WHERE BuildingTypeId = 3);

-- Даємо старим містам джерело дерева, не перевищуючи ліміт міста.
INSERT INTO CityBuilding (BuildingId, CityId, BuildingTypeId, Level, Status, SpentGold, SpentWood, SpentStone)
SELECT lower(hex(randomblob(16))), c.CityId, 3, 1, 'Active', 0, 0, 0 FROM City c
WHERE NOT EXISTS (SELECT 1 FROM CityBuilding b WHERE b.CityId = c.CityId AND b.BuildingTypeId = 3)
  AND (SELECT COUNT(*) FROM CityBuilding b WHERE b.CityId = c.CityId) < 50;
