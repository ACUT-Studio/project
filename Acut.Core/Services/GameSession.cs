using Acut.Core.Abstractions;
using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Combat;
using Acut.Core.Domain.Economy;

namespace Acut.Core.Services;

/// <summary>Точка входу в логіку гри: стан, гравець, місто, активний бій.</summary>
public sealed class GameSession
{
    private static readonly Dictionary<GameState, GameState[]> AllowedTransitions = new()
    {
        [GameState.MainMenu] = [GameState.CharacterCreation, GameState.City],
        [GameState.CharacterCreation] = [GameState.City, GameState.MainMenu],
        [GameState.City] = [GameState.Battle, GameState.Paused],
        [GameState.Paused] = [GameState.City, GameState.MainMenu],
        [GameState.Battle] = [GameState.Victory, GameState.Defeat],
        [GameState.Victory] = [GameState.City],
        [GameState.Defeat] = [GameState.City, GameState.MainMenu],
    };

    private SaveManager? _saveManager;

    /// <summary>Повний конструктор: використовується фабрикою нової гри та репозиторієм при завантаженні.</summary>
    public GameSession(
        Guid sessionId, DateTime startedAt, GameState state,
        Player player, City city, GameEventDispatcher dispatcher)
    {
        SessionId = sessionId;
        StartedAt = startedAt;
        CurrentState = state;
        Player = player ?? throw new ArgumentNullException(nameof(player));
        City = city ?? throw new ArgumentNullException(nameof(city));
        Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Dispatcher.Attach(player);
    }

    public Guid SessionId { get; }
    public GameState CurrentState { get; private set; }
    public DateTime StartedAt { get; }
    public Player Player { get; }
    public City City { get; }
    public GameEventDispatcher Dispatcher { get; }
    public Battle? ActiveBattle { get; private set; }

    /// <summary>Створює нову гру: гравець, каталог навичок/завдань/досягнень, місто, біржа. Завершується у стані City.</summary>
    /// <exception cref="GameRuleException">Некоректне ім'я персонажа.</exception>
    public static GameSession StartNewGame(
        string playerName, AppearanceSettings? appearance, ICatalogRepository catalog,
        GameEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var name = playerName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new GameRuleException("Введіть ім'я персонажа.");
        if (name.Length > GameConfig.MaxPlayerNameLength)
            throw new GameRuleException($"Ім'я не може бути довшим за {GameConfig.MaxPlayerNameLength} символів.");

        dispatcher ??= new GameEventDispatcher();

        var player = new Player(name, appearance ?? AppearanceSettings.Default, gold: GameConfig.StartingGold);
        player.SkillTree.Initialize(catalog.GetSkills());
        player.SkillTree.SkillPoints = GameConfig.StartingSkillPoints;
        player.Achievements.Initialize(catalog.GetAchievements());
        foreach (var quest in catalog.GetQuests())
            player.QuestLog.AddQuest(quest);

        var city = new City($"Місто {name}", player, dispatcher, market: new StockMarket(catalog.GetStocks()));
        var session = new GameSession(Guid.NewGuid(), DateTime.UtcNow, GameState.CharacterCreation, player, city, dispatcher);
        session.ChangeState(GameState.City);
        return session;
    }

    public void AttachSaveManager(SaveManager saveManager) =>
        _saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));

    public void ChangeState(GameState newState)
    {
        if (!AllowedTransitions.TryGetValue(CurrentState, out var allowed) || !allowed.Contains(newState))
            throw new InvalidOperationException($"Перехід {CurrentState} → {newState} неможливий.");

        CurrentState = newState;
        if (newState == GameState.City)
            ActiveBattle = null;
    }

    /// <summary>Починає бій з ворогом: City → Battle.</summary>
    public Battle StartBattle(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ChangeState(GameState.Battle);
        ActiveBattle = new Battle(Player, enemy, Dispatcher);
        ActiveBattle.Start();
        return ActiveBattle;
    }

    /// <summary>Призначає бій, що тривав на момент збереження (завантаження).</summary>
    public void RestoreBattle(Battle battle) => ActiveBattle = battle ?? throw new ArgumentNullException(nameof(battle));

    /// <summary>Тік симуляції. Працює лише в місті; у стані «Пауза» таймер не діє.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (CurrentState != GameState.City) return;
        City.Tick();
        Player.Statistics.AddPlayTime(elapsed);
    }

    public void Save()
    {
        if (_saveManager is null)
            throw new InvalidOperationException("Сесію не зареєстровано в SaveManager.");
        _saveManager.SaveGame(this);
    }
}
