# UML-діаграми проєкту AcutStudio

Діаграми написані в **Mermaid** і автоматично відображаються на GitHub. Імена класів і методів відповідають C#/.NET (PascalCase).

Зміст:

1. [Діаграма класів](#1-діаграма-класів)
   - 1.1 Ядро гри та персонажі
   - 1.2 Бойова система
   - 1.3 Місто, економіка та предмети
   - 1.4 Прогрес: навички, завдання, досягнення, статистика
2. [Діаграми станів](#2-діаграми-станів)
   - 2.1 Стани гри
   - 2.2 Життєвий цикл бою
   - 2.3 Життєвий цикл будівлі
   - 2.4 Життєвий цикл завдання

---

## 1. Діаграма класів

Позначення зв'язків:

| Нотація | Зв'язок |
| :--- | :--- |
| `A <\|-- B` | Наслідування (B успадковує A) |
| `A *-- B` | Композиція (B не існує без A) |
| `A o-- B` | Агрегація (B може існувати окремо) |
| `A --> B` | Асоціація (A використовує / знає про B) |
| `A ..> B` | Залежність |
| `A ..\|> B` | Реалізація інтерфейсу |

### 1.1 Ядро гри та персонажі

```mermaid
classDiagram
    direction TB

    class GameSession {
        +Guid SessionId
        +GameState CurrentState
        +DateTime StartedAt
        +StartNewGame(string name) void
        +ChangeState(GameState newState) void
        +Save() void
        +Load(Guid saveId) void
    }

    class Character {
        <<abstract>>
        +string Name
        +int Health
        +int MaxHealth
        +int AttackPower
        +int Defense
        +bool IsAlive
        +TakeDamage(int amount) void
        +Heal(int amount) void
    }

    class Player {
        +int Level
        +int Experience
        +int Gold
        +AppearanceSettings Appearance
        +GainExperience(int xp) void
        +LevelUp() void
        +AddGold(int amount) void
        +SpendGold(int amount) bool
    }

    class Enemy {
        +EnemyType Type
        +int RewardGold
        +int RewardExperience
        +IEnemyStrategy Strategy
        +ChooseAction(Battle battle) BattleAction
    }

    class AppearanceSettings {
        +string HairStyle
        +string SkinColor
        +string Outfit
    }

    class Inventory {
        +int Capacity
        +AddItem(Item item, int qty) bool
        +RemoveItem(Item item, int qty) bool
        +HasItem(Item item) bool
        +GetItems() List~ItemStack~
    }

    class ItemStack {
        +Item Item
        +int Quantity
    }

    class SaveManager {
        +SaveGame(GameSession session) void
        +LoadGame(Guid saveId) GameSession
        +AutoSave() void
    }

    class EnemyType {
        <<enumeration>>
        Goblin
        Wolf
        Bandit
        Boss
    }

    GameSession "1" *-- "1" Player
    GameSession "1" *-- "1" City
    GameSession "1" o-- "0..1" Battle : активний бій
    GameSession ..> SaveManager

    Character <|-- Player
    Character <|-- Enemy

    Player "1" *-- "1" Inventory
    Player "1" *-- "1" AppearanceSettings
    Player "1" *-- "1" SkillTree
    Player "1" *-- "1" QuestLog
    Player "1" *-- "1" AchievementTracker
    Player "1" *-- "1" PlayerStatistics

    Inventory "1" o-- "*" ItemStack
    ItemStack "*" --> "1" Item

    Enemy --> EnemyType
```

### 1.2 Бойова система

```mermaid
classDiagram
    direction LR

    class Battle {
        +Player Player
        +Enemy Enemy
        +int TurnNumber
        +BattleState State
        +bool IsPlayerTurn
        +Start() void
        +ExecutePlayerAction(BattleAction action, Item item) ActionResult
        +ExecuteEnemyAction() ActionResult
        +NextTurn() void
        +CheckOutcome() BattleResult
    }

    class BattleAction {
        <<enumeration>>
        Attack
        Defend
        Heal
        UseItem
    }

    class BattleResult {
        <<enumeration>>
        InProgress
        Victory
        Defeat
    }

    class ActionResult {
        +BattleAction Action
        +int Damage
        +int HealedAmount
        +string Message
    }

    class IEnemyStrategy {
        <<interface>>
        +ChooseAction(Enemy self, Player target) BattleAction
    }

    class AggressiveStrategy {
        +ChooseAction(Enemy self, Player target) BattleAction
    }

    class DefensiveStrategy {
        +ChooseAction(Enemy self, Player target) BattleAction
    }

    class BalancedStrategy {
        +ChooseAction(Enemy self, Player target) BattleAction
    }

    class Character {
        <<abstract>>
    }
    class Player
    class Enemy
    class Item {
        <<abstract>>
    }

    Battle "1" --> "1" Player
    Battle "1" --> "1" Enemy
    Battle ..> BattleAction
    Battle ..> BattleResult
    Battle ..> ActionResult
    Battle ..> Item : UseItem

    Enemy "1" --> "1" IEnemyStrategy
    IEnemyStrategy <|.. AggressiveStrategy
    IEnemyStrategy <|.. DefensiveStrategy
    IEnemyStrategy <|.. BalancedStrategy

    Character <|-- Player
    Character <|-- Enemy
```

### 1.3 Місто, економіка та предмети

```mermaid
classDiagram
    direction TB

    class City {
        +string Name
        +int CityLevel
        +int Population
        +BuildBuilding(BuildingType type) Building
        +GetBuildings() List~Building~
        +CollectAllIncome() int
        +Tick() void
    }

    class Building {
        <<abstract>>
        +Guid Id
        +string Name
        +int Level
        +int MaxLevel
        +int BuildCost
        +int UpgradeCost
        +int IncomePerTick
        +BuildingStatus Status
        +Build() void
        +Upgrade() bool
        +CollectIncome() int
    }

    class ResidentialBuilding {
        +int Capacity
    }
    class ProductionBuilding {
        +string Resource
        +int ProductionRate
    }
    class WorkshopBuilding {
        +Craft(Recipe recipe) Item
    }
    class MarketBuilding {
        +StockMarket Market
    }

    class BuildingStatus {
        <<enumeration>>
        Locked
        Available
        UnderConstruction
        Active
        Upgrading
        MaxLevel
    }

    class StockMarket {
        +List~Stock~ Stocks
        +Buy(Stock stock, int qty, Player p) bool
        +Sell(Stock stock, int qty, Player p) bool
        +UpdatePrices() void
    }

    class Stock {
        +string Symbol
        +decimal Price
        +decimal Volatility
        +Fluctuate() void
    }

    class Item {
        <<abstract>>
        +Guid Id
        +string Name
        +string Description
        +int Price
        +int Level
        +Upgrade() bool
    }

    class Potion {
        +PotionEffect Effect
        +int EffectValue
        +Use(Character target) void
    }
    class Weapon {
        +int BonusAttack
    }
    class Armor {
        +int BonusDefense
    }

    class Recipe {
        +string Name
        +Dictionary~Item, int~ Ingredients
        +Potion Result
        +CanCraft(Inventory inv) bool
    }

    class Shop {
        +List~Item~ Assortment
        +BuyItem(Item item, Player p) bool
        +UpgradeItem(Item item, Player p) bool
    }

    class Player
    class Inventory

    City "1" *-- "*" Building
    Building <|-- ResidentialBuilding
    Building <|-- ProductionBuilding
    Building <|-- WorkshopBuilding
    Building <|-- MarketBuilding
    Building --> BuildingStatus

    MarketBuilding "1" *-- "1" StockMarket
    StockMarket "1" o-- "*" Stock

    Item <|-- Potion
    Item <|-- Weapon
    Item <|-- Armor

    WorkshopBuilding ..> Recipe
    Recipe "1" --> "1" Potion : створює
    Recipe "*" o-- "*" Item : інгредієнти

    Shop "1" o-- "*" Item
    Shop ..> Player
    Player ..> Inventory
```

### 1.4 Прогрес: навички, завдання, досягнення, статистика

```mermaid
classDiagram
    direction TB

    class SkillTree {
        +List~Skill~ Skills
        +int SkillPoints
        +Unlock(Skill skill) bool
        +CanUnlock(Skill skill) bool
    }

    class Skill {
        +string Name
        +string Description
        +int Cost
        +bool IsUnlocked
        +List~Skill~ Prerequisites
        +BuildingType[] UnlocksBuildings
    }

    class QuestLog {
        +List~Quest~ ActiveQuests
        +List~Quest~ CompletedQuests
        +AddQuest(Quest q) void
        +UpdateProgress(GameEvent e) void
        +ClaimReward(Quest q, Player p) void
    }

    class Quest {
        +string Title
        +string Description
        +QuestStatus Status
        +int Progress
        +int Target
        +int RewardGold
        +int RewardExperience
        +IsCompleted() bool
    }

    class QuestStatus {
        <<enumeration>>
        NotStarted
        InProgress
        Completed
        RewardClaimed
    }

    class AchievementTracker {
        +List~Achievement~ Achievements
        +CheckTriggers(GameEvent e) void
    }

    class Achievement {
        +string Title
        +string Condition
        +bool IsUnlocked
        +DateTime? UnlockedAt
        +Unlock() void
    }

    class PlayerStatistics {
        +int BattlesWon
        +int BattlesLost
        +int BuildingsBuilt
        +int TotalGoldEarned
        +int PotionsCrafted
        +TimeSpan PlayTime
        +Record(GameEvent e) void
    }

    class GameEvent {
        +GameEventType Type
        +int Value
        +DateTime Timestamp
    }

    SkillTree "1" *-- "*" Skill
    Skill "*" --> "*" Skill : вимагає

    QuestLog "1" *-- "*" Quest
    Quest --> QuestStatus

    AchievementTracker "1" *-- "*" Achievement

    QuestLog ..> GameEvent
    AchievementTracker ..> GameEvent
    PlayerStatistics ..> GameEvent
```

### Коротко про ключові рішення

- **Наслідування**: `Character` → `Player`, `Enemy`; `Building` → 4 типи; `Item` → `Potion`, `Weapon`, `Armor`. Нові типи додаються без зміни наявного коду (вимога масштабованості).
- **Композиція**: `GameSession` володіє `Player` і `City`; `Player` володіє `Inventory`, `SkillTree`, `QuestLog` тощо; `City` володіє `Building`.
- **Агрегація**: `Inventory` → `ItemStack`, `Shop` → `Item`, `StockMarket` → `Stock` (об'єкти існують незалежно від контейнера).
- **Стратегія (Strategy)**: `IEnemyStrategy` задає унікальну поведінку ворогів у бою.
- **Події**: `GameEvent` розв'язує завдання, досягнення й статистику від решти логіки (єдине джерело тригерів).

---

## 2. Діаграми станів

### 2.1 Стани гри

```mermaid
stateDiagram-v2
    direction TB

    [*] --> MainMenu

    state "Головне меню" as MainMenu
    state "Створення персонажа" as CharacterCreation
    state "Місто" as CityView {
        [*] --> Idle
        state "Огляд міста" as Idle
        state "Процес будівництва" as Construction
        state "Управління: магазин, крафтинг, навички, завдання, біржа" as Management

        Idle --> Construction : Обрано будівлю та є валюта
        Construction --> Idle : Будівництво підтверджено або скасовано
        Idle --> Management : Відкрито вікно управління
        Management --> Idle : Вікно закрито
    }
    state "Бій" as Battle
    state "Перемога" as Victory
    state "Поразка" as Defeat
    state "Пауза" as Paused

    MainMenu --> CharacterCreation : Нова гра
    MainMenu --> CityView : Продовжити (завантажено збереження)
    MainMenu --> [*] : Вихід

    CharacterCreation --> CityView : Персонаж створено та збережено
    CharacterCreation --> MainMenu : Скасувати

    CityView --> Battle : Обрано ворога
    CityView --> Paused : Натиснуто Esc
    Paused --> CityView : Продовжити
    Paused --> MainMenu : Зберегти та вийти в меню

    Battle --> Victory : HP ворога ≤ 0
    Battle --> Defeat : HP гравця ≤ 0

    Victory --> CityView : Нагороду отримано
    Defeat --> CityView : Гравця відновлено (штраф до валюти)
    Defeat --> MainMenu : Вихід у меню
```

**Таблиця переходів (стани гри)**

| Звідки | Куди | Тригер | Умова / дія |
| :--- | :--- | :--- | :--- |
| Початок | Головне меню | Запуск застосунку | Ініціалізація ресурсів |
| Головне меню | Створення персонажа | Кнопка «Нова гра» | Створюється нова `GameSession` |
| Головне меню | Місто | Кнопка «Продовжити» | `SaveManager.LoadGame()` успішний |
| Створення персонажа | Місто | Підтвердження | Ім'я та параметри валідні, збереження |
| Місто (огляд) | Місто (будівництво) | Вибір будівлі | `Player.Gold ≥ BuildCost`, будівля `Available` |
| Місто | Бій | Вибір ворога | Створюється `Battle` |
| Бій | Перемога | `Enemy.Health ≤ 0` | Нарахування нагород, оновлення статистики |
| Бій | Поразка | `Player.Health ≤ 0` | Фіксація поразки |
| Перемога / Поразка | Місто | Кнопка «Продовжити» | Автозбереження |
| Місто | Пауза | Клавіша Esc | Зупинка тіків симуляції |

### 2.2 Життєвий цикл бою

```mermaid
stateDiagram-v2
    direction TB

    [*] --> Initialized : Створено Battle
    state "Бій ініціалізовано" as Initialized
    state "Хід гравця: очікування вибору дії" as PlayerTurn
    state "Обробка дії гравця" as ResolvePlayer
    state "Хід ворога: вибір дії за стратегією" as EnemyTurn
    state "Обробка дії ворога" as ResolveEnemy
    state check_enemy <<choice>>
    state check_player <<choice>>
    state "Перемога" as Victory
    state "Поразка" as Defeat

    Initialized --> PlayerTurn : Start()

    PlayerTurn --> ResolvePlayer : Обрано Attack, Defend, Heal або UseItem
    ResolvePlayer --> check_enemy : Дію виконано

    check_enemy --> Victory : HP ворога ≤ 0
    check_enemy --> EnemyTurn : HP ворога > 0

    EnemyTurn --> ResolveEnemy : Стратегія обрала дію
    ResolveEnemy --> check_player : Дію виконано

    check_player --> Defeat : HP гравця ≤ 0
    check_player --> PlayerTurn : HP гравця > 0 (TurnNumber++)

    Victory --> [*] : Нагорода: валюта та досвід
    Defeat --> [*] : Штраф і повернення в місто
```

### 2.3 Життєвий цикл будівлі

```mermaid
stateDiagram-v2
    direction LR

    [*] --> Locked : Будівлю додано до каталогу

    state "Заблокована" as Locked
    state "Доступна для купівлі" as Available
    state "Будується" as UnderConstruction
    state "Активна (приносить дохід)" as Active
    state "Покращується" as Upgrading
    state "Максимальний рівень" as MaxLevel

    Locked --> Available : Розблоковано навичку в дереві
    Available --> UnderConstruction : Купівля (списання валюти)
    UnderConstruction --> Active : Будівництво завершено
    Active --> Upgrading : Upgrade() і достатньо валюти
    Upgrading --> Active : Покращення завершено, Level++
    Active --> MaxLevel : Level = MaxLevel
    MaxLevel --> [*] : Гра завершена
```

### 2.4 Життєвий цикл завдання

```mermaid
stateDiagram-v2
    direction LR

    [*] --> NotStarted

    state "Не розпочато" as NotStarted
    state "Виконується" as InProgress
    state "Виконано" as Completed
    state "Нагороду отримано" as RewardClaimed

    NotStarted --> InProgress : Завдання прийнято
    InProgress --> InProgress : GameEvent збільшує Progress
    InProgress --> Completed : Progress ≥ Target
    Completed --> RewardClaimed : ClaimReward()
    RewardClaimed --> [*]
```

---