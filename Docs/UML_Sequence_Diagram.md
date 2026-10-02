## Учасники

| Учасник | Тип | Роль |
| :--- | :--- | :--- |
| Гравець | Актор | Взаємодіє з інтерфейсом мишею та клавіатурою |
| UI | Boundary | Форми WinForms/WPF: меню, місто, бій |
| GameSession | Control | Керує станом гри та поточною сесією |
| SaveManager | Сервіс | Збереження й завантаження (БД / файл) |
| City, Building, Shop | Entity | Місто, будівлі, магазин |
| Player, Inventory | Entity | Гравець та його інвентар |
| Battle, Enemy, IEnemyStrategy | Entity | Бій, ворог і його стратегія |
| SkillTree, QuestLog, AchievementTracker, PlayerStatistics | Entity | Прогрес гравця |

## Покриття прецедентів

| № | Сценарій | Прецедент | Розділ |
| :--- | :--- | :--- | :--- |
| 1 | Нова гра та створення персонажа | Меню | 1.1 |
| 2 | Продовження гри (завантаження) | Меню | 1.2 |
| 3 | Будівництво будівлі | Місто | 2.1 |
| 4 | Крафтинг зілля | Місто | 2.2 |
| 5 | Розблокування навички | Місто | 2.3 |
| 6 | Бій: повний цикл | Бій | 3.1 |
| 7 | Обробка ігрових подій (завдання, досягнення, статистика) | Наскрізний | 4 |

Умовні позначення: суцільна стрілка `->>` — виклик, пунктирна `-->>` — повернення результату, прямокутник на лінії життя — час виконання операції (activation), хрестик — знищення об'єкта.

---

## 1. Головне меню

### 1.1 Нова гра та створення персонажа

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant GS as GameSession
    participant SM as SaveManager

    Гравець->>UI: Натискає «Нова гра»
    UI->>UI: Показує екран створення персонажа
    Гравець->>UI: Вводить ім'я, обирає характеристики і вигляд
    Гравець->>UI: Натискає «Підтвердити»

    UI->>GS: StartNewGame(name)
    activate GS
    alt Дані валідні
        create participant P as Player
        GS->>P: new Player(name, appearance)
        create participant C as City
        GS->>C: new City()
        GS->>SM: SaveGame(session)
        activate SM
        SM-->>GS: OK
        deactivate SM
        GS->>GS: ChangeState(CityView)
        GS-->>UI: Сесію створено
        UI-->>Гравець: Відкрито екран міста
    else Порожнє ім'я або некоректні параметри
        GS-->>UI: Помилка валідації
        UI-->>Гравець: Повідомлення про помилку
    end
    deactivate GS
```

### 1.2 Продовження гри (завантаження)

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant GS as GameSession
    participant SM as SaveManager

    Гравець->>UI: Натискає «Продовжити»
    UI->>GS: Load(saveId)
    activate GS
    GS->>SM: LoadGame(saveId)
    activate SM
    alt Збереження знайдено та цілісне
        SM-->>GS: GameSession (Player, City, Inventory, прогрес)
        deactivate SM
        GS->>GS: ChangeState(CityView)
        GS-->>UI: Стан відновлено
        UI-->>Гравець: Екран міста з відновленим прогресом
    else Файл відсутній або пошкоджений
        SM-->>GS: Помилка читання
        GS-->>UI: LoadFailed
        UI-->>Гравець: Повідомлення, повернення в меню
    end
    deactivate GS
```

---

## 2. Місто

### 2.1 Будівництво будівлі

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant GS as GameSession
    participant City
    participant P as Player
    participant EV as GameEvent

    Гравець->>UI: Обирає тип будівлі у меню будівництва
    UI->>City: BuildBuilding(type)
    activate City
    City->>P: SpendGold(BuildCost)
    activate P
    alt Достатньо валюти і будівля Available
        P-->>City: true
        deactivate P
        create participant B as Building
        City->>B: new Building(type)
        City->>B: Build()
        activate B
        Note over B: Status = UnderConstruction
        B-->>City: Будівництво розпочато
        deactivate B
        City-->>UI: Building
        UI-->>Гравець: Будівля з'являється на карті
        City->>EV: BuildingBuilt
        Note over B: Через N тіків Status = Active
        loop Кожен Tick симуляції
            GS->>City: Tick()
            City->>B: CollectIncome()
            B-->>City: IncomePerTick
            City->>P: AddGold(income)
        end
    else Недостатньо валюти або будівля Locked
        P-->>City: false
        City-->>UI: Відмова
        UI-->>Гравець: «Недостатньо коштів» або «Будівля заблокована»
    end
    deactivate City
```

### 2.2 Крафтинг зілля

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant W as WorkshopBuilding
    participant R as Recipe
    participant Inv as Inventory

    Гравець->>UI: Обирає рецепт у майстерні
    UI->>W: Craft(recipe)
    activate W
    W->>R: CanCraft(inventory)
    activate R
    R->>Inv: HasItem(ingredient, qty)
    Inv-->>R: true або false
    R-->>W: Результат перевірки
    deactivate R
    alt Інгредієнтів достатньо
        loop Для кожного інгредієнта
            W->>Inv: RemoveItem(ingredient, qty)
        end
        create participant Potion
        W->>Potion: new Potion(effect, value)
        W->>Inv: AddItem(potion, 1)
        W-->>UI: Зілля створено
        UI-->>Гравець: Оновлений інвентар
    else Інгредієнтів не вистачає
        W-->>UI: Відмова
        UI-->>Гравець: «Недостатньо матеріалів»
    end
    deactivate W
```

### 2.3 Розблокування навички

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant ST as SkillTree
    participant S as Skill
    participant B as Building

    Гравець->>UI: Обирає навичку в дереві
    UI->>ST: Unlock(skill)
    activate ST
    ST->>ST: CanUnlock(skill)
    Note right of ST: Перевірка SkillPoints та Prerequisites
    alt Умови виконано
        ST->>S: IsUnlocked = true
        ST->>B: Status = Available (для UnlocksBuildings)
        ST-->>UI: OK
        UI-->>Гравець: Навичку розблоковано, нові будівлі доступні
    else Умови не виконано
        ST-->>UI: false
        UI-->>Гравець: Навичка заблокована
    end
    deactivate ST
```

---

## 3. Бій

### 3.1 Повний цикл бою

```mermaid
sequenceDiagram
    autonumber
    actor Гравець
    participant UI
    participant GS as GameSession
    participant P as Player
    participant E as Enemy
    participant Strat as IEnemyStrategy
    participant Inv as Inventory

    Гравець->>UI: Обирає ворога в місті
    UI->>GS: StartBattle(enemyType)
    activate GS
    create participant Battle
    GS->>Battle: new Battle(player, enemy)
    GS->>GS: ChangeState(Battle)
    GS->>Battle: Start()
    activate Battle
    Note over Battle: Життєвий цикл Battle: від створення до знищення

    loop Поки BattleResult = InProgress
        Battle-->>UI: Очікування ходу гравця
        UI-->>Гравець: Кнопки: Атака, Захист, Відновлення, Предмет

        Гравець->>UI: Обирає дію
        UI->>Battle: ExecutePlayerAction(action, item)
        alt Attack
            Battle->>E: TakeDamage(P.AttackPower - E.Defense)
        else Defend
            Battle->>P: Тимчасово підвищує Defense
        else Heal
            Battle->>P: Heal(amount)
        else UseItem
            Battle->>Inv: RemoveItem(item, 1)
            Battle->>P: item.Use(target)
        end
        Battle-->>UI: ActionResult
        UI-->>Гравець: Оновлені HP та повідомлення

        Battle->>Battle: CheckOutcome()
        opt HP ворога ≤ 0
            Note over Battle: BattleResult = Victory, вихід із циклу
        end

        Battle->>E: ChooseAction(battle)
        E->>Strat: ChooseAction(self, player)
        Strat-->>E: BattleAction
        E-->>Battle: BattleAction
        Battle->>P: TakeDamage(E.AttackPower - P.Defense)
        Battle-->>UI: ActionResult
        UI-->>Гравець: Оновлені HP та повідомлення

        Battle->>Battle: CheckOutcome()
        Battle->>Battle: NextTurn()
    end

    alt Victory
        Battle-->>GS: BattleResult.Victory
        GS->>P: AddGold(RewardGold)
        GS->>P: GainExperience(RewardExperience)
        GS-->>UI: Екран перемоги з нагородою
    else Defeat
        Battle-->>GS: BattleResult.Defeat
        GS->>P: Відновлення HP, штраф до валюти
        GS-->>UI: Екран поразки
    end
    deactivate Battle
    destroy Battle
    GS->>Battle: Dispose()
    GS->>GS: ChangeState(CityView)
    GS->>GS: AutoSave()
    GS-->>UI: Повернення до міста
    deactivate GS
    UI-->>Гравець: Екран міста
```

---

## 4. Наскрізний процес: обробка ігрових подій

Після кожної значущої дії (побудовано будівлю, виграно бій, створено зілля) генерується `GameEvent`. Три підсистеми обробляють його незалежно одна від одної.

```mermaid
sequenceDiagram
    autonumber
    participant Src as Джерело події (City, Battle, Workshop)
    participant EV as GameEvent
    participant QL as QuestLog
    participant AT as AchievementTracker
    participant PS as PlayerStatistics
    participant UI

    Src->>EV: new GameEvent(type, value)
    par Завдання
        EV->>QL: UpdateProgress(event)
        activate QL
        QL->>QL: Quest.Progress += value
        opt Progress ≥ Target
            Note over QL: Status = Completed
            QL-->>UI: Завдання виконано
        end
        deactivate QL
    and Досягнення
        EV->>AT: CheckTriggers(event)
        activate AT
        opt Умову досягнення виконано
            AT->>AT: Achievement.Unlock()
            AT-->>UI: Нове досягнення
        end
        deactivate AT
    and Статистика
        EV->>PS: Record(event)
        activate PS
        PS->>PS: Оновлення лічильників
        deactivate PS
    end
    UI->>UI: Оновлення сторінки завдань і статистики
```