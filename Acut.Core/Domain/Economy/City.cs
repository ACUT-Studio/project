using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;

namespace Acut.Core.Domain.Economy;

public sealed class City
{
    private readonly Player _owner;
    private readonly GameEventDispatcher _dispatcher;
    private readonly List<Building> _buildings = new();

    public City(string name, Player owner, GameEventDispatcher dispatcher, int cityLevel = 1, StockMarket? market = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва міста не може бути порожньою.", nameof(name));
        ArgumentOutOfRangeException.ThrowIfLessThan(cityLevel, 1);

        Name = name.Trim();
        CityLevel = cityLevel;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Market = market ?? new StockMarket();
    }

    public string Name { get; }
    public int CityLevel { get; }
    public StockMarket Market { get; }

    /// <summary>Населення: сума місткості житлових будівель з урахуванням рівня. Обчислюється, не вводиться.</summary>
    public int Population => _buildings.OfType<ResidentialBuilding>()
        .Where(b => b.Status is BuildingStatus.Active or BuildingStatus.MaxLevel)
        .Sum(b => b.Capacity * b.Level);

    public List<Building> GetBuildings() => _buildings.ToList();

    /// <summary>Чи розблокована будівля навичками гравця (Locked/Available не зберігаються в БД).</summary>
    public BuildingStatus GetAvailability(BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.RequiredSkillId is int skillId && !_owner.SkillTree.IsUnlocked(skillId)
            ? BuildingStatus.Locked
            : BuildingStatus.Available;
    }

    /// <summary>Будує будівлю: перевіряє навичку, списує валюту, публікує подію BuildingBuilt.</summary>
    /// <exception cref="GameRuleException">Будівля заблокована або не вистачає валюти.</exception>
    public Building BuildBuilding(BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (GetAvailability(type) == BuildingStatus.Locked)
            throw new GameRuleException($"Будівля «{type.Name}» заблокована: спершу вивчіть потрібну навичку.");
        if (!_owner.SpendGold(type.BuildCost))
            throw new GameRuleException($"Недостатньо валюти: потрібно {type.BuildCost}, є {_owner.Gold}.");

        var building = BuildingFactory.Create(type);
        building.Build();
        AddBuilding(building);
        _dispatcher.Publish(new GameEvent(GameEventType.BuildingBuilt));
        return building;
    }

    /// <summary>Додає вже збудовану будівлю (завантаження збереження). Події та оплата не виконуються.</summary>
    public void RestoreBuilding(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);
        AddBuilding(building);
    }

    /// <summary>Нараховує дохід усіх активних будівель за один тік. Повертає суму.</summary>
    public int CollectAllIncome()
    {
        var total = _buildings.Sum(b => b.CollectIncome());
        if (total <= 0) return 0;

        _owner.AddGold(total);
        _dispatcher.Publish(new GameEvent(GameEventType.GoldEarned, total));
        return total;
    }

    /// <summary>Один тік симуляції. Викликається з UI за таймером; у стані «Пауза» не викликається.</summary>
    public void Tick() => CollectAllIncome();

    private void AddBuilding(Building building)
    {
        if (building is MarketBuilding market) market.Market = Market;
        _buildings.Add(building);
    }
}
