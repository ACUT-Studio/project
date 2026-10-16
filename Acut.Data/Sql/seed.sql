-- AcutStudio: початкове наповнення каталогу контенту (баланс - за погодженням з Game Designer).
-- Нову будівлю, предмет, ворога тощо додає один INSERT: код змінювати не потрібно.

-- Навички
INSERT INTO Skill (SkillId, Name, Description, Cost) VALUES
    (1, 'Основи будівництва', 'Відкриває доступ до кам''яних будинків і шахт.', 1),
    (2, 'Ремесла',            'Відкриває майстерню для крафту зілля.',            2),
    (3, 'Торгівля',           'Відкриває ринок з біржею акцій.',                  2);

INSERT INTO SkillPrerequisite (SkillId, RequiredSkillId) VALUES
    (2, 1),
    (3, 1);

-- Будівлі
INSERT INTO BuildingType
    (BuildingTypeId, Kind, Name, BuildCost, UpgradeCost, MaxLevel, IncomePerTick, Capacity, Resource, ProductionRate, RequiredSkillId) VALUES
    (1, 'Residential', 'Хатина',           100, 80,  3, 2,  4,    NULL,   NULL, NULL),
    (2, 'Residential', 'Кам''яний будинок', 250, 200, 3, 5, 10,    NULL,   NULL, 1),
    (3, 'Production',  'Ферма',            150, 120, 3, 4,  NULL, 'Food', 1,    NULL),
    (4, 'Production',  'Шахта',            400, 300, 3, 10, NULL, 'Ore',  1,    1),
    (5, 'Workshop',    'Майстерня зілля',  600, 450, 3, 0,  NULL, NULL,   NULL, 2),
    (6, 'Market',      'Ринок',            800, 600, 3, 6,  NULL, NULL,   NULL, 3);

-- Предмети
INSERT INTO ItemDefinition
    (ItemId, Kind, Name, Description, Price, BonusAttack, BonusDefense, PotionEffect, EffectValue) VALUES
    (1, 'Potion', 'Мале зілля лікування',   'Відновлює 25 здоров''я.',  30,  0, 0, 'Heal', 25),
    (2, 'Potion', 'Велике зілля лікування', 'Відновлює 60 здоров''я.',  80,  0, 0, 'Heal', 60),
    (3, 'Potion', 'Еліксир відновлення',    'Відновлює 120 здоров''я.', 200, 0, 0, 'Heal', 120),
    (4, 'Weapon', 'Залізний меч',           'Додає 5 до атаки.',        150, 5, 0, NULL, 0),
    (5, 'Weapon', 'Сталевий меч',           'Додає 12 до атаки.',       400, 12, 0, NULL, 0),
    (6, 'Armor',  'Шкіряна броня',          'Додає 3 до захисту.',      120, 0, 3, NULL, 0),
    (7, 'Armor',  'Залізна броня',          'Додає 8 до захисту.',      350, 0, 8, NULL, 0);

-- Рецепти крафту зілля
INSERT INTO Recipe (RecipeId, Name, ResultItemId) VALUES
    (1, 'Велике зілля лікування', 2),
    (2, 'Еліксир відновлення',    3);

INSERT INTO RecipeIngredient (RecipeId, ItemId, Quantity) VALUES
    (1, 1, 2),
    (2, 2, 1),
    (2, 1, 1);

-- Завдання
INSERT INTO QuestDefinition (QuestId, Title, Description, EventType, Target, RewardGold, RewardExperience) VALUES
    (1, 'Перша будівля', 'Збудуйте першу будівлю в місті.',       'BuildingBuilt', 1,    50,  20),
    (2, 'Містобудівник', 'Збудуйте 5 будівель.',                  'BuildingBuilt', 5,    200, 80),
    (3, 'Перша кров',    'Переможіть ворога в бою.',              'BattleWon',     1,    100, 50),
    (4, 'Заробіток',     'Зароби 1000 одиниць валюти.',           'GoldEarned',    1000, 150, 60),
    (5, 'Алхімік',       'Виготовте 3 зілля.',                    'PotionCrafted', 3,    120, 60);

-- Досягнення
INSERT INTO AchievementDefinition (AchievementId, Title, Condition, EventType, Threshold) VALUES
    (1, 'Перша перемога', 'Виграйте 1 бій.',                'BattleWon',     1),
    (2, 'Ветеран',        'Виграйте 10 боїв.',              'BattleWon',     10),
    (3, 'Забудовник',     'Збудуйте 10 будівель.',          'BuildingBuilt', 10),
    (4, 'Багатій',        'Зароби 10000 одиниць валюти.',   'GoldEarned',    10000),
    (5, 'Знавець зілля',  'Виготовте 10 зілля.',            'PotionCrafted', 10);

-- Вороги
INSERT INTO EnemyDefinition
    (EnemyId, EnemyType, Name, MaxHealth, AttackPower, Defense, RewardGold, RewardExperience, StrategyType) VALUES
    (1, 'Goblin', 'Гоблін',           30,  6,  1, 20,  15,  'Aggressive'),
    (2, 'Wolf',   'Вовк',             45,  9,  2, 30,  25,  'Balanced'),
    (3, 'Bandit', 'Бандит',           60,  11, 4, 50,  40,  'Defensive'),
    (4, 'Boss',   'Ватажок розбійників', 150, 18, 8, 200, 150, 'Balanced');

-- Акції (ціни в сотих)
INSERT INTO Stock (StockId, Symbol, Name, BasePrice, Volatility) VALUES
    (1, 'AGRO', 'Агро-Холдинг', 10000, 0.05),
    (2, 'MINE', 'ГірМаш',       25000, 0.10),
    (3, 'ALCH', 'Алхімія+',      5000, 0.15);
