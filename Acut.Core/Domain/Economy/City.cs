using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Items;
using Acut.Core.Domain.Progress;
using Acut.Core.Services;

namespace Acut.Core.Domain.Economy;

public sealed class City
{
    private readonly Player _owner;
    private readonly GameEventDispatcher _dispatcher;
    private readonly List<Building> _buildings = new();

    public City(string name, Player owner, GameEventDispatcher dispatcher, int cityLevel = 1, StockMarket? market = null, Shop? shop = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва міста не може бути порожньою.", nameof(name));
        ArgumentOutOfRangeException.ThrowIfLessThan(cityLevel, 1);

        Name = name.Trim();
        CityLevel = cityLevel;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Market = market ?? new StockMarket();
        Market.IsAccessible = () => HasMarket;
        Shop = shop;
        if (Shop is not null) Shop.IsAccessible = () => HasMarket;
    }

    public string Name { get; }
    public int CityLevel { get; }
    public StockMarket Market { get; }
    public Shop? Shop { get; }
    public bool HasWorkshop => _buildings.Any(b => b.Kind == BuildingKind.Workshop && b.Status is BuildingStatus.Active or BuildingStatus.MaxLevel);
    public bool HasMarket => _buildings.Any(b => b.Kind == BuildingKind.Market && b.Status is BuildingStatus.Active or BuildingStatus.MaxLevel);

    public Item? Craft(Recipe recipe)
    {
        var workshop = _buildings.OfType<WorkshopBuilding>().FirstOrDefault()
            ?? throw new GameRuleException("Для зіллєваріння побудуйте майстерню зілля.");
        return workshop.Craft(recipe, _owner.Inventory);
    }

    public string? GetBuildFailureReason(BuildingType type)
    {
        if (!CanBuildMore) return $"Досягнуто ліміту: {GameConfig.MaxCityBuildings} будівель.";
        if (type.Kind is BuildingKind.Market or BuildingKind.Workshop && _buildings.Any(b => b.Kind == type.Kind))
            return $"Будівля «{type.Name}» вже є в місті: дозволено лише одну.";
        if (GetAvailability(type) == BuildingStatus.Locked)
            return $"Будівля «{type.Name}» заблокована: спершу вивчіть потрібну навичку.";
        if (!_owner.CanAfford(type.BuildResources)) return "Недостатньо ресурсів для будівництва.";
        return null;
    }

    public bool CanBuildMore => _buildings.Count < GameConfig.MaxCityBuildings;

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

        var reason = GetBuildFailureReason(type);
        if (reason is not null) throw new GameRuleException(reason);
        if (!_owner.SpendResources(type.BuildResources))
            throw new GameRuleException("Недостатньо ресурсів для будівництва.");
        var building = BuildingFactory.Create(type);
        building.Build();
        AddBuilding(building);
        _dispatcher.Publish(new GameEvent(GameEventType.BuildingBuilt));
        return building;
    }

    /// <summary>Зносить власну будівлю та повертає половину фактичних витрат кожного ресурсу. Повторне знесення неможливе.</summary>
    public ResourceAmounts RemoveBuilding(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);
        if (!_buildings.Remove(building))
            throw new GameRuleException($"Будівлю «{building.Name}» не знайдено в місті.");

        var refund = building.DemolitionResources;
        _owner.AddResources(refund);
        return refund;
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

        if (!_owner.CanAfford(building.UpgradeResources))
        {
            reason = "Недостатньо ресурсів для покращення.";
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

        if (!_owner.SpendResources(building.UpgradeResources))
            throw new GameRuleException("Недостатньо ресурсів для покращення.");

        if (!building.Upgrade())
        {
            _owner.AddResources(building.UpgradeResources);
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
    public int CollectAllIncome() => CollectAllResources().Gold;

    public ResourceAmounts CollectAllResources()
    {
        var total = _buildings.Aggregate(new ResourceAmounts(), (sum, b) => sum.Add(b.OutputPerTick));
        _owner.AddResources(total);
        if (total.Gold > 0) _dispatcher.Publish(new GameEvent(GameEventType.GoldEarned, total.Gold));
        return total;
    }
    /// <summary>Один тік симуляції. Викликається з UI за таймером; у стані «Пауза» не викликається.</summary>
    public void Tick()
    {
        CollectAllIncome();
        if (HasMarket) Market.UpdatePrices();
    }

    private void AddBuilding(Building building)
    {
        if (building is MarketBuilding market) market.Market = Market;
        if (building is WorkshopBuilding workshop)
        {
            workshop.Dispatcher = _dispatcher;
            workshop.IsAccessible = () => _buildings.Contains(workshop);
        }
        _buildings.Add(building);
    }
}
