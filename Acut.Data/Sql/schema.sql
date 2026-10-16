-- AcutStudio: схема БД, версія 1 (PRAGMA user_version = 1).
-- Відповідає діаграмі сутностей і зв'язків з Docs/Architecture.md, розділ 6.
-- Правила: Guid і дата/час - TEXT, enum - TEXT з CHECK, гроші й ціни акцій - INTEGER (акції в сотих).

------------------------------------------------------------------
-- КАТАЛОГ (однаковий для всіх збережень, наповнюється seed.sql)
------------------------------------------------------------------

CREATE TABLE Skill (
    SkillId     INTEGER PRIMARY KEY,
    Name        TEXT    NOT NULL UNIQUE,
    Description TEXT    NOT NULL DEFAULT '',
    Cost        INTEGER NOT NULL CHECK (Cost >= 0)
);

CREATE TABLE SkillPrerequisite (
    SkillId         INTEGER NOT NULL REFERENCES Skill (SkillId) ON DELETE CASCADE,
    RequiredSkillId INTEGER NOT NULL REFERENCES Skill (SkillId) ON DELETE CASCADE,
    PRIMARY KEY (SkillId, RequiredSkillId),
    CHECK (SkillId <> RequiredSkillId)
);

CREATE TABLE BuildingType (
    BuildingTypeId  INTEGER PRIMARY KEY,
    Kind            TEXT    NOT NULL CHECK (Kind IN ('Residential', 'Production', 'Workshop', 'Market')),
    Name            TEXT    NOT NULL UNIQUE,
    BuildCost       INTEGER NOT NULL CHECK (BuildCost >= 0),
    UpgradeCost     INTEGER NOT NULL CHECK (UpgradeCost >= 0),
    MaxLevel        INTEGER NOT NULL CHECK (MaxLevel >= 1),
    IncomePerTick   INTEGER NOT NULL DEFAULT 0 CHECK (IncomePerTick >= 0),
    Capacity        INTEGER CHECK (Capacity IS NULL OR Capacity >= 0),
    Resource        TEXT,
    ProductionRate  INTEGER CHECK (ProductionRate IS NULL OR ProductionRate >= 0),
    RequiredSkillId INTEGER REFERENCES Skill (SkillId),
    CHECK (Kind <> 'Residential' OR Capacity IS NOT NULL),
    CHECK (Kind <> 'Production'  OR (Resource IS NOT NULL AND ProductionRate IS NOT NULL))
);

CREATE TABLE ItemDefinition (
    ItemId       INTEGER PRIMARY KEY,
    Kind         TEXT    NOT NULL CHECK (Kind IN ('Potion', 'Weapon', 'Armor')),
    Name         TEXT    NOT NULL UNIQUE,
    Description  TEXT    NOT NULL DEFAULT '',
    Price        INTEGER NOT NULL CHECK (Price >= 0),
    BonusAttack  INTEGER NOT NULL DEFAULT 0 CHECK (BonusAttack >= 0),
    BonusDefense INTEGER NOT NULL DEFAULT 0 CHECK (BonusDefense >= 0),
    PotionEffect TEXT    CHECK (PotionEffect IS NULL OR PotionEffect IN ('Heal')),
    EffectValue  INTEGER NOT NULL DEFAULT 0 CHECK (EffectValue >= 0),
    CHECK (Kind <> 'Potion' OR PotionEffect IS NOT NULL)
);

CREATE TABLE Recipe (
    RecipeId     INTEGER PRIMARY KEY,
    Name         TEXT    NOT NULL UNIQUE,
    ResultItemId INTEGER NOT NULL REFERENCES ItemDefinition (ItemId)
);

CREATE TABLE RecipeIngredient (
    RecipeId INTEGER NOT NULL REFERENCES Recipe (RecipeId) ON DELETE CASCADE,
    ItemId   INTEGER NOT NULL REFERENCES ItemDefinition (ItemId),
    Quantity INTEGER NOT NULL CHECK (Quantity >= 1),
    PRIMARY KEY (RecipeId, ItemId)
);

CREATE TABLE QuestDefinition (
    QuestId          INTEGER PRIMARY KEY,
    Title            TEXT    NOT NULL UNIQUE,
    Description      TEXT    NOT NULL DEFAULT '',
    EventType        TEXT    NOT NULL CHECK (EventType IN (
        'BuildingBuilt', 'BuildingUpgraded', 'GoldEarned', 'BattleWon',
        'BattleLost', 'PotionCrafted', 'ItemBought', 'SkillUnlocked')),
    Target           INTEGER NOT NULL CHECK (Target >= 1),
    RewardGold       INTEGER NOT NULL DEFAULT 0 CHECK (RewardGold >= 0),
    RewardExperience INTEGER NOT NULL DEFAULT 0 CHECK (RewardExperience >= 0)
);

CREATE TABLE AchievementDefinition (
    AchievementId INTEGER PRIMARY KEY,
    Title         TEXT    NOT NULL UNIQUE,
    Condition     TEXT    NOT NULL DEFAULT '',
    EventType     TEXT    NOT NULL CHECK (EventType IN (
        'BuildingBuilt', 'BuildingUpgraded', 'GoldEarned', 'BattleWon',
        'BattleLost', 'PotionCrafted', 'ItemBought', 'SkillUnlocked')),
    Threshold     INTEGER NOT NULL CHECK (Threshold >= 1)
);

CREATE TABLE EnemyDefinition (
    EnemyId          INTEGER PRIMARY KEY,
    EnemyType        TEXT    NOT NULL CHECK (EnemyType IN ('Goblin', 'Wolf', 'Bandit', 'Boss')),
    Name             TEXT    NOT NULL UNIQUE,
    MaxHealth        INTEGER NOT NULL CHECK (MaxHealth >= 1),
    AttackPower      INTEGER NOT NULL CHECK (AttackPower >= 0),
    Defense          INTEGER NOT NULL CHECK (Defense >= 0),
    RewardGold       INTEGER NOT NULL DEFAULT 0 CHECK (RewardGold >= 0),
    RewardExperience INTEGER NOT NULL DEFAULT 0 CHECK (RewardExperience >= 0),
    StrategyType     TEXT    NOT NULL CHECK (StrategyType IN ('Aggressive', 'Defensive', 'Balanced'))
);

CREATE TABLE Stock (
    StockId    INTEGER PRIMARY KEY,
    Symbol     TEXT    NOT NULL UNIQUE,
    Name       TEXT    NOT NULL,
    BasePrice  INTEGER NOT NULL CHECK (BasePrice > 0),   -- у сотих
    Volatility REAL    NOT NULL CHECK (Volatility >= 0)
);

------------------------------------------------------------------
-- СТАН ГРИ (прив'язаний до GameSave, видалення збереження каскадне)
------------------------------------------------------------------

CREATE TABLE GameSave (
    SaveId       TEXT PRIMARY KEY,
    Name         TEXT NOT NULL,
    CreatedAt    TEXT NOT NULL,
    UpdatedAt    TEXT NOT NULL,
    CurrentState TEXT NOT NULL CHECK (CurrentState IN (
        'MainMenu', 'CharacterCreation', 'City', 'Battle', 'Victory', 'Defeat', 'Paused'))
);

CREATE TABLE Player (
    PlayerId    INTEGER PRIMARY KEY AUTOINCREMENT,
    SaveId      TEXT    NOT NULL UNIQUE REFERENCES GameSave (SaveId) ON DELETE CASCADE,
    Name        TEXT    NOT NULL,
    Level       INTEGER NOT NULL CHECK (Level >= 1),
    Experience  INTEGER NOT NULL CHECK (Experience >= 0),
    Gold        INTEGER NOT NULL CHECK (Gold >= 0),
    Health      INTEGER NOT NULL,
    MaxHealth   INTEGER NOT NULL CHECK (MaxHealth >= 1),
    AttackPower INTEGER NOT NULL CHECK (AttackPower >= 0),
    Defense     INTEGER NOT NULL CHECK (Defense >= 0),
    SkillPoints INTEGER NOT NULL CHECK (SkillPoints >= 0),
    HairStyle   TEXT    NOT NULL,
    SkinColor   TEXT    NOT NULL,
    Outfit      TEXT    NOT NULL,
    CHECK (Health BETWEEN 0 AND MaxHealth)
);

CREATE TABLE City (
    CityId     INTEGER PRIMARY KEY AUTOINCREMENT,
    SaveId     TEXT    NOT NULL UNIQUE REFERENCES GameSave (SaveId) ON DELETE CASCADE,
    Name       TEXT    NOT NULL,
    CityLevel  INTEGER NOT NULL CHECK (CityLevel >= 1),
    Population INTEGER NOT NULL CHECK (Population >= 0)
);

CREATE TABLE CityBuilding (
    BuildingId     TEXT    PRIMARY KEY,
    CityId         INTEGER NOT NULL REFERENCES City (CityId) ON DELETE CASCADE,
    BuildingTypeId INTEGER NOT NULL REFERENCES BuildingType (BuildingTypeId),
    Level          INTEGER NOT NULL CHECK (Level >= 1),
    Status         TEXT    NOT NULL CHECK (Status IN ('UnderConstruction', 'Active', 'Upgrading', 'MaxLevel'))
);

CREATE TABLE InventoryItem (
    PlayerId  INTEGER NOT NULL REFERENCES Player (PlayerId) ON DELETE CASCADE,
    ItemId    INTEGER NOT NULL REFERENCES ItemDefinition (ItemId),
    Quantity  INTEGER NOT NULL CHECK (Quantity >= 1),
    ItemLevel INTEGER NOT NULL DEFAULT 1 CHECK (ItemLevel >= 1),
    PRIMARY KEY (PlayerId, ItemId)
);

CREATE TABLE PlayerSkill (
    PlayerId   INTEGER NOT NULL REFERENCES Player (PlayerId) ON DELETE CASCADE,
    SkillId    INTEGER NOT NULL REFERENCES Skill (SkillId),
    UnlockedAt TEXT    NOT NULL,
    PRIMARY KEY (PlayerId, SkillId)
);

CREATE TABLE PlayerQuest (
    PlayerId INTEGER NOT NULL REFERENCES Player (PlayerId) ON DELETE CASCADE,
    QuestId  INTEGER NOT NULL REFERENCES QuestDefinition (QuestId),
    Status   TEXT    NOT NULL CHECK (Status IN ('NotStarted', 'InProgress', 'Completed', 'RewardClaimed')),
    Progress INTEGER NOT NULL DEFAULT 0 CHECK (Progress >= 0),
    PRIMARY KEY (PlayerId, QuestId)
);

CREATE TABLE PlayerAchievement (
    PlayerId      INTEGER NOT NULL REFERENCES Player (PlayerId) ON DELETE CASCADE,
    AchievementId INTEGER NOT NULL REFERENCES AchievementDefinition (AchievementId),
    UnlockedAt    TEXT    NOT NULL,
    PRIMARY KEY (PlayerId, AchievementId)
);

CREATE TABLE PlayerStatistics (
    PlayerId        INTEGER PRIMARY KEY REFERENCES Player (PlayerId) ON DELETE CASCADE,
    BattlesWon      INTEGER NOT NULL DEFAULT 0 CHECK (BattlesWon >= 0),
    BattlesLost     INTEGER NOT NULL DEFAULT 0 CHECK (BattlesLost >= 0),
    BuildingsBuilt  INTEGER NOT NULL DEFAULT 0 CHECK (BuildingsBuilt >= 0),
    TotalGoldEarned INTEGER NOT NULL DEFAULT 0 CHECK (TotalGoldEarned >= 0),
    PotionsCrafted  INTEGER NOT NULL DEFAULT 0 CHECK (PotionsCrafted >= 0),
    PlayTimeSeconds INTEGER NOT NULL DEFAULT 0 CHECK (PlayTimeSeconds >= 0)
);

CREATE TABLE ActiveBattle (
    SaveId       TEXT    PRIMARY KEY REFERENCES GameSave (SaveId) ON DELETE CASCADE,
    EnemyId      INTEGER NOT NULL REFERENCES EnemyDefinition (EnemyId),
    EnemyHealth  INTEGER NOT NULL CHECK (EnemyHealth >= 0),
    TurnNumber   INTEGER NOT NULL CHECK (TurnNumber >= 1),
    IsPlayerTurn INTEGER NOT NULL CHECK (IsPlayerTurn IN (0, 1))
);

CREATE TABLE SaveStock (
    SaveId       TEXT    NOT NULL REFERENCES GameSave (SaveId) ON DELETE CASCADE,
    StockId      INTEGER NOT NULL REFERENCES Stock (StockId),
    CurrentPrice INTEGER NOT NULL CHECK (CurrentPrice > 0),   -- у сотих
    PRIMARY KEY (SaveId, StockId)
);

CREATE TABLE PlayerStock (
    PlayerId INTEGER NOT NULL REFERENCES Player (PlayerId) ON DELETE CASCADE,
    StockId  INTEGER NOT NULL REFERENCES Stock (StockId),
    Quantity INTEGER NOT NULL CHECK (Quantity >= 1),
    PRIMARY KEY (PlayerId, StockId)
);

------------------------------------------------------------------
-- Індекси для зовнішніх ключів, за якими йде вибірка
------------------------------------------------------------------
CREATE INDEX IX_CityBuilding_CityId      ON CityBuilding (CityId);
CREATE INDEX IX_BuildingType_SkillId     ON BuildingType (RequiredSkillId);
CREATE INDEX IX_InventoryItem_ItemId     ON InventoryItem (ItemId);
CREATE INDEX IX_PlayerQuest_QuestId      ON PlayerQuest (QuestId);
CREATE INDEX IX_GameSave_UpdatedAt       ON GameSave (UpdatedAt);
