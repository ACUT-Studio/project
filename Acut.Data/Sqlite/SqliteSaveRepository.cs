using Acut.Core.Abstractions;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;
using Acut.Data.Mapping;
using Microsoft.Data.Sqlite;

namespace Acut.Data.Sqlite;

/// <summary>
/// Запис і читання стану гри. Запис виконується однією транзакцією: або зберігається все, або нічого
/// (незавершена транзакція відкочується при Dispose, попереднє збереження лишається цілим).
/// </summary>
public sealed class SqliteSaveRepository : ISaveRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ICatalogRepository _catalog;

    public SqliteSaveRepository(SqliteConnectionFactory factory, ICatalogRepository catalog)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    // ------------------------------------------------------------------ Save

    public void Save(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var connection = _factory.CreateConnection();
        using var tx = connection.BeginTransaction();

        var saveId = session.SessionId.ToString();
        var player = session.Player;

        UpsertGameSave(connection, tx, session, saveId);
        var playerId = UpsertPlayer(connection, tx, player, saveId);
        var cityId = UpsertCity(connection, tx, session.City, saveId);

        ReplaceBuildings(connection, tx, session.City, cityId);
        ReplaceInventory(connection, tx, player, playerId);
        SaveSkills(connection, tx, player, playerId);
        ReplaceQuests(connection, tx, player, playerId);
        SaveAchievements(connection, tx, player, playerId);
        UpsertStatistics(connection, tx, player, playerId);
        ReplaceBattle(connection, tx, session, saveId);
        ReplaceStocks(connection, tx, session, player, saveId, playerId);

        tx.Commit();
    }

    private static void UpsertGameSave(SqliteConnection c, SqliteTransaction tx, GameSession session, string saveId) =>
        SqlHelper.Execute(c, tx, """
            INSERT INTO GameSave (SaveId, Name, CreatedAt, UpdatedAt, CurrentState)
            VALUES ($id, $name, $created, $updated, $state)
            ON CONFLICT(SaveId) DO UPDATE SET
                Name = excluded.Name,
                UpdatedAt = excluded.UpdatedAt,
                CurrentState = excluded.CurrentState
            """,
            ("$id", saveId),
            ("$name", $"{session.Player.Name} (рівень {session.Player.Level})"),
            ("$created", SaveMapper.FormatDate(session.StartedAt)),
            ("$updated", SaveMapper.FormatDate(DateTime.UtcNow)),
            ("$state", session.CurrentState.ToString()));

    private static long UpsertPlayer(SqliteConnection c, SqliteTransaction tx, Player p, string saveId)
    {
        SqlHelper.Execute(c, tx, """
            INSERT INTO Player (SaveId, Name, Level, Experience, Gold, Health, MaxHealth, AttackPower,
                                Defense, SkillPoints, HairStyle, SkinColor, Outfit)
            VALUES ($save, $name, $level, $exp, $gold, $health, $maxHealth, $attack,
                    $defense, $points, $hair, $skin, $outfit)
            ON CONFLICT(SaveId) DO UPDATE SET
                Name = excluded.Name, Level = excluded.Level, Experience = excluded.Experience,
                Gold = excluded.Gold, Health = excluded.Health, MaxHealth = excluded.MaxHealth,
                AttackPower = excluded.AttackPower, Defense = excluded.Defense,
                SkillPoints = excluded.SkillPoints, HairStyle = excluded.HairStyle,
                SkinColor = excluded.SkinColor, Outfit = excluded.Outfit
            """,
            ("$save", saveId), ("$name", p.Name), ("$level", p.Level), ("$exp", p.Experience),
            ("$gold", p.Gold), ("$health", p.Health), ("$maxHealth", p.MaxHealth),
            ("$attack", p.AttackPower), ("$defense", p.Defense), ("$points", p.SkillPoints),
            ("$hair", p.Appearance.HairStyle), ("$skin", p.Appearance.SkinColor), ("$outfit", p.Appearance.Outfit));

        return Convert.ToInt64(SqlHelper.Scalar(c, tx, "SELECT PlayerId FROM Player WHERE SaveId = $save", ("$save", saveId)));
    }

    private static long UpsertCity(SqliteConnection c, SqliteTransaction tx, City city, string saveId)
    {
        SqlHelper.Execute(c, tx, """
            INSERT INTO City (SaveId, Name, CityLevel, Population)
            VALUES ($save, $name, $level, $population)
            ON CONFLICT(SaveId) DO UPDATE SET
                Name = excluded.Name, CityLevel = excluded.CityLevel, Population = excluded.Population
            """,
            ("$save", saveId), ("$name", city.Name), ("$level", city.CityLevel), ("$population", city.Population));

        return Convert.ToInt64(SqlHelper.Scalar(c, tx, "SELECT CityId FROM City WHERE SaveId = $save", ("$save", saveId)));
    }

    private static void ReplaceBuildings(SqliteConnection c, SqliteTransaction tx, City city, long cityId)
    {
        SqlHelper.Execute(c, tx, "DELETE FROM CityBuilding WHERE CityId = $city", ("$city", cityId));
        foreach (var b in city.GetBuildings())
        {
            SqlHelper.Execute(c, tx, """
                INSERT INTO CityBuilding (BuildingId, CityId, BuildingTypeId, Level, Status)
                VALUES ($id, $city, $type, $level, $status)
                """,
                ("$id", b.Id.ToString()), ("$city", cityId), ("$type", b.TypeId),
                ("$level", b.Level), ("$status", b.Status.ToString()));
        }
    }

    private static void ReplaceInventory(SqliteConnection c, SqliteTransaction tx, Player player, long playerId)
    {
        SqlHelper.Execute(c, tx, "DELETE FROM InventoryItem WHERE PlayerId = $p", ("$p", playerId));
        foreach (var stack in player.Inventory.GetItems())
        {
            SqlHelper.Execute(c, tx, """
                INSERT INTO InventoryItem (PlayerId, ItemId, Quantity, ItemLevel)
                VALUES ($p, $item, $qty, $level)
                """,
                ("$p", playerId), ("$item", stack.Item.Id), ("$qty", stack.Quantity), ("$level", stack.Item.Level));
        }
    }

    /// <summary>Навички не «забуваються», тому лише додаємо нові (час вивчення зберігається з першого запису).</summary>
    private static void SaveSkills(SqliteConnection c, SqliteTransaction tx, Player player, long playerId)
    {
        var now = SaveMapper.FormatDate(DateTime.UtcNow);
        foreach (var skill in player.SkillTree.Skills.Where(s => s.IsUnlocked))
        {
            SqlHelper.Execute(c, tx,
                "INSERT OR IGNORE INTO PlayerSkill (PlayerId, SkillId, UnlockedAt) VALUES ($p, $skill, $at)",
                ("$p", playerId), ("$skill", skill.Id), ("$at", now));
        }
    }

    private static void ReplaceQuests(SqliteConnection c, SqliteTransaction tx, Player player, long playerId)
    {
        SqlHelper.Execute(c, tx, "DELETE FROM PlayerQuest WHERE PlayerId = $p", ("$p", playerId));
        foreach (var q in player.QuestLog.AllQuests)
        {
            SqlHelper.Execute(c, tx, """
                INSERT INTO PlayerQuest (PlayerId, QuestId, Status, Progress)
                VALUES ($p, $quest, $status, $progress)
                """,
                ("$p", playerId), ("$quest", q.Id), ("$status", q.Status.ToString()), ("$progress", q.Progress));
        }
    }

    private static void SaveAchievements(SqliteConnection c, SqliteTransaction tx, Player player, long playerId)
    {
        foreach (var a in player.Achievements.Achievements.Where(a => a.IsUnlocked))
        {
            SqlHelper.Execute(c, tx, """
                INSERT OR IGNORE INTO PlayerAchievement (PlayerId, AchievementId, UnlockedAt)
                VALUES ($p, $a, $at)
                """,
                ("$p", playerId), ("$a", a.Id), ("$at", SaveMapper.FormatDate(a.UnlockedAt ?? DateTime.UtcNow)));
        }
    }

    private static void UpsertStatistics(SqliteConnection c, SqliteTransaction tx, Player player, long playerId)
    {
        var s = player.Statistics;
        SqlHelper.Execute(c, tx, """
            INSERT INTO PlayerStatistics (PlayerId, BattlesWon, BattlesLost, BuildingsBuilt,
                                          TotalGoldEarned, PotionsCrafted, PlayTimeSeconds)
            VALUES ($p, $won, $lost, $built, $gold, $potions, $seconds)
            ON CONFLICT(PlayerId) DO UPDATE SET
                BattlesWon = excluded.BattlesWon, BattlesLost = excluded.BattlesLost,
                BuildingsBuilt = excluded.BuildingsBuilt, TotalGoldEarned = excluded.TotalGoldEarned,
                PotionsCrafted = excluded.PotionsCrafted, PlayTimeSeconds = excluded.PlayTimeSeconds
            """,
            ("$p", playerId), ("$won", s.BattlesWon), ("$lost", s.BattlesLost), ("$built", s.BuildingsBuilt),
            ("$gold", s.TotalGoldEarned), ("$potions", s.PotionsCrafted), ("$seconds", (long)s.PlayTime.TotalSeconds));
    }

    private static void ReplaceBattle(SqliteConnection c, SqliteTransaction tx, GameSession session, string saveId)
    {
        SqlHelper.Execute(c, tx, "DELETE FROM ActiveBattle WHERE SaveId = $save", ("$save", saveId));
        var battle = session.ActiveBattle;
        if (battle is null) return;

        SqlHelper.Execute(c, tx, """
            INSERT INTO ActiveBattle (SaveId, EnemyId, EnemyHealth, TurnNumber, IsPlayerTurn)
            VALUES ($save, $enemy, $health, $turn, $isPlayerTurn)
            """,
            ("$save", saveId), ("$enemy", battle.Enemy.DefinitionId), ("$health", battle.Enemy.Health),
            ("$turn", battle.TurnNumber), ("$isPlayerTurn", battle.IsPlayerTurn ? 1 : 0));
    }

    private static void ReplaceStocks(
        SqliteConnection c, SqliteTransaction tx, GameSession session, Player player, string saveId, long playerId)
    {
        SqlHelper.Execute(c, tx, "DELETE FROM SaveStock WHERE SaveId = $save", ("$save", saveId));
        foreach (var stock in session.City.Market.Stocks)
        {
            SqlHelper.Execute(c, tx,
                "INSERT INTO SaveStock (SaveId, StockId, CurrentPrice) VALUES ($save, $stock, $price)",
                ("$save", saveId), ("$stock", stock.Id), ("$price", SaveMapper.ToCents(stock.Price)));
        }

        SqlHelper.Execute(c, tx, "DELETE FROM PlayerStock WHERE PlayerId = $p", ("$p", playerId));
        foreach (var (stockId, quantity) in player.StockHoldings.Where(h => h.Value > 0))
        {
            SqlHelper.Execute(c, tx,
                "INSERT INTO PlayerStock (PlayerId, StockId, Quantity) VALUES ($p, $stock, $qty)",
                ("$p", playerId), ("$stock", stockId), ("$qty", quantity));
        }
    }

    // ------------------------------------------------------------------ Load

    public GameSession? Load(Guid saveId)
    {
        using var connection = _factory.CreateConnection();
        var id = saveId.ToString();

        var createdRaw = SqlHelper.Scalar(connection, null, "SELECT CreatedAt FROM GameSave WHERE SaveId = $id", ("$id", id));
        if (createdRaw is not string createdAt) return null;

        // Довідкові дані. Кожен виклик повертає нові об'єкти, які нижче отримують стан збереження.
        var items = _catalog.GetItems().ToDictionary(i => i.Id);
        var skills = _catalog.GetSkills();
        var quests = _catalog.GetQuests().ToDictionary(q => q.Id);
        var buildingTypes = _catalog.GetBuildingTypes().ToDictionary(t => t.Id);
        var enemies = _catalog.GetEnemies().ToDictionary(e => e.Id);
        var stocks = _catalog.GetStocks();

        Player? player = null;
        long playerId = 0;
        var skillPoints = 0;
        SqlHelper.Read(connection, "SELECT * FROM Player WHERE SaveId = $id", r =>
        {
            playerId = r.GetLong("PlayerId");
            skillPoints = r.GetInt("SkillPoints");
            player = SaveMapper.ReadPlayer(r);
        }, ("$id", id));
        if (player is null)
            throw new InvalidDataException("Збереження пошкоджене: відсутній запис гравця.");

        LoadPlayerProgress(connection, player, playerId, skills, skillPoints, items, quests);
        var market = LoadMarket(connection, id, stocks);

        var dispatcher = new GameEventDispatcher();
        var city = LoadCity(connection, player, dispatcher, market, id, buildingTypes);
        var battle = LoadBattle(connection, player, dispatcher, id, enemies);

        var state = battle is null ? GameState.City : GameState.Battle;
        var session = new GameSession(saveId, SaveMapper.ParseDate(createdAt), state, player, city, dispatcher);
        if (battle is not null) session.RestoreBattle(battle);
        return session;
    }

    private void LoadPlayerProgress(
        SqliteConnection connection, Player player, long playerId, IReadOnlyList<Skill> skills, int skillPoints,
        Dictionary<int, Item> items, Dictionary<int, Quest> quests)
    {
        var p = ("$p", (object?)playerId);

        player.SkillTree.Initialize(skills);
        player.SkillTree.SkillPoints = skillPoints;
        player.SkillTree.RestoreUnlocked(
            SqlHelper.Query(connection, "SELECT SkillId FROM PlayerSkill WHERE PlayerId = $p", r => r.GetInt("SkillId"), p));

        SqlHelper.Read(connection, "SELECT ItemId, Quantity, ItemLevel FROM InventoryItem WHERE PlayerId = $p", r =>
        {
            if (!items.TryGetValue(r.GetInt("ItemId"), out var item)) return; // предмет прибрано з каталогу
            item.RestoreLevel(r.GetInt("ItemLevel"));
            player.Inventory.AddItem(item, r.GetInt("Quantity"));
        }, p);

        var restoredQuests = new List<Quest>();
        SqlHelper.Read(connection, "SELECT QuestId, Status, Progress FROM PlayerQuest WHERE PlayerId = $p", r =>
        {
            if (!quests.TryGetValue(r.GetInt("QuestId"), out var quest)) return;
            quest.Restore(r.GetEnum<QuestStatus>("Status"), r.GetInt("Progress"));
            restoredQuests.Add(quest);
        }, p);
        player.QuestLog.Restore(restoredQuests);

        var achievements = _catalog.GetAchievements().ToDictionary(a => a.Id);
        SqlHelper.Read(connection, "SELECT AchievementId, UnlockedAt FROM PlayerAchievement WHERE PlayerId = $p", r =>
        {
            if (achievements.TryGetValue(r.GetInt("AchievementId"), out var a))
                a.Restore(SaveMapper.ParseDate(r.GetStr("UnlockedAt")));
        }, p);
        player.Achievements.Initialize(achievements.Values);

        SqlHelper.Read(connection, "SELECT * FROM PlayerStatistics WHERE PlayerId = $p", r =>
            player.Statistics.Restore(
                r.GetInt("BattlesWon"), r.GetInt("BattlesLost"), r.GetInt("BuildingsBuilt"),
                r.GetInt("TotalGoldEarned"), r.GetInt("PotionsCrafted"),
                TimeSpan.FromSeconds(r.GetLong("PlayTimeSeconds"))), p);

        SqlHelper.Read(connection, "SELECT StockId, Quantity FROM PlayerStock WHERE PlayerId = $p", r =>
            player.StockHoldings[r.GetInt("StockId")] = r.GetInt("Quantity"), p);
    }

    private static StockMarket LoadMarket(SqliteConnection connection, string saveId, IReadOnlyList<Stock> stocks)
    {
        var byId = stocks.ToDictionary(s => s.Id);
        SqlHelper.Read(connection, "SELECT StockId, CurrentPrice FROM SaveStock WHERE SaveId = $save", r =>
        {
            if (byId.TryGetValue(r.GetInt("StockId"), out var stock))
                stock.RestorePrice(SaveMapper.FromCents(r.GetLong("CurrentPrice")));
        }, ("$save", saveId));
        return new StockMarket(stocks);
    }

    private static City LoadCity(
        SqliteConnection connection, Player player, GameEventDispatcher dispatcher, StockMarket market,
        string saveId, Dictionary<int, BuildingType> buildingTypes)
    {
        City? city = null;
        long cityId = 0;
        SqlHelper.Read(connection, "SELECT CityId, Name, CityLevel FROM City WHERE SaveId = $save", r =>
        {
            cityId = r.GetLong("CityId");
            city = new City(r.GetStr("Name"), player, dispatcher, r.GetInt("CityLevel"), market);
        }, ("$save", saveId));
        if (city is null)
            throw new InvalidDataException("Збереження пошкоджене: відсутній запис міста.");

        var loadedCity = city;
        SqlHelper.Read(connection,
            "SELECT BuildingId, BuildingTypeId, Level, Status FROM CityBuilding WHERE CityId = $city ORDER BY rowid", r =>
            {
                if (!buildingTypes.TryGetValue(r.GetInt("BuildingTypeId"), out var type)) return;
                var building = BuildingFactory.Create(type);
                building.Restore(Guid.Parse(r.GetStr("BuildingId")), r.GetInt("Level"), r.GetEnum<BuildingStatus>("Status"));
                loadedCity.RestoreBuilding(building);
            }, ("$city", cityId));

        return loadedCity;
    }

    private static Battle? LoadBattle(
        SqliteConnection connection, Player player, GameEventDispatcher dispatcher, string saveId,
        Dictionary<int, EnemyDefinition> enemies)
    {
        Battle? battle = null;
        SqlHelper.Read(connection,
            "SELECT EnemyId, EnemyHealth, TurnNumber, IsPlayerTurn FROM ActiveBattle WHERE SaveId = $save", r =>
            {
                if (!enemies.TryGetValue(r.GetInt("EnemyId"), out var definition)) return;
                battle = new Battle(player, definition.CreateEnemy(r.GetInt("EnemyHealth")), dispatcher);
                battle.Restore(r.GetInt("TurnNumber"), r.GetInt("IsPlayerTurn") == 1);
            }, ("$save", saveId));
        return battle;
    }

    // ------------------------------------------------------------------ Інше

    public IReadOnlyList<SaveSlotInfo> ListSaves()
    {
        using var connection = _factory.CreateConnection();
        return SqlHelper.Query(connection,
            "SELECT SaveId, Name, UpdatedAt FROM GameSave ORDER BY UpdatedAt DESC",
            r => new SaveSlotInfo(Guid.Parse(r.GetStr("SaveId")), r.GetStr("Name"), SaveMapper.ParseDate(r.GetStr("UpdatedAt"))));
    }

    /// <summary>Видалення каскадне: разом зі збереженням зникає весь його стан.</summary>
    public void Delete(Guid saveId)
    {
        using var connection = _factory.CreateConnection();
        SqlHelper.Execute(connection, null, "DELETE FROM GameSave WHERE SaveId = $id", ("$id", saveId.ToString()));
    }
}
