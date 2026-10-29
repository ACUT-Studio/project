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

    /// <summary>Чи можна покращити будівлю: перевіряє наявність, рівень, статус і наявність валюти.</summary>
    public bool CanUpgradeBuilding(Building building, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(building);

        if (!_buildings.Contains(building))
        {
            reason = $"Будівлю «{building.Name}» не знайдено в місті.";
            return false;
        }

        if (building.Level >= building.MaxLevel || building.Status == BuildingStatus.MaxLevel)
        {
            reason = $"Будівля «{building.Name}» вже досягла максимального рівня ({building.MaxLevel}).";
            return false;
        }

        if (building.Status != BuildingStatus.Active)
        {
            reason = $"Будівлю «{building.Name}» не можна покращити в поточному стані ({building.Status}).";
            return false;
        }

        if (_owner.Gold < building.UpgradeCost)
        {
            reason = $"Недостатньо валюти: потрібно {building.UpgradeCost}, є {_owner.Gold}.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Спроба покращення будівлі без викидання винятку.</summary>
    public bool TryUpgradeBuilding(Building building, out string? failureReason)
    {
        if (!CanUpgradeBuilding(building, out failureReason))
            return false;

        return UpgradeBuilding(building);
    }

    /// <summary>
    /// Покращує будівлю: перевіряє статус і рівень, списує UpgradeCost,
    /// змінює стан (Active → Upgrading → Active / MaxLevel) і публікує подію BuildingUpgraded.
    /// </summary>
    /// <exception cref="ArgumentNullException">Будівлю не передано.</exception>
    /// <exception cref="GameRuleException">Будівля не знайдена, вже на MaxLevel, неактивна або не вистачає валюти.</exception>
    public bool UpgradeBuilding(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        if (!CanUpgradeBuilding(building, out var reason))
            throw new GameRuleException(reason!);

        if (!_owner.SpendGold(building.UpgradeCost))
            throw new GameRuleException($"Недостатньо валюти: потрібно {building.UpgradeCost}, є {_owner.Gold}.");

        if (!building.Upgrade())
        {
            _owner.AddGold(building.UpgradeCost);
            return false;
        }

        _dispatcher.Publish(new GameEvent(GameEventType.BuildingUpgraded));
        return true;
    }

    /// <summary>Покращує будівлю за її ID.</summary>
    public bool UpgradeBuilding(Guid buildingId)
    {
        var building = _buildings.FirstOrDefault(b => b.Id == buildingId)
            ?? throw new GameRuleException($"Будівлю з ідентифікатором {buildingId} не знайдено в місті.");
        return UpgradeBuilding(building);
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
