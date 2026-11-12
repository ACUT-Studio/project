namespace Acut.Core.Domain.Economy;

public abstract class Building
{
    private readonly int _baseIncomePerTick;

    protected Building(BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Id = Guid.NewGuid();
        TypeId = type.Id;
        Name = type.Name;
        MaxLevel = type.MaxLevel;
        BuildCost = type.BuildCost;
        UpgradeCost = type.UpgradeCost;
        BuildResources = type.BuildResources;
        UpgradeResources = type.UpgradeResources;
        ProducedResource = type.Kind is BuildingKind.Market or BuildingKind.Workshop ? null
            : type.Kind == BuildingKind.Residential ? ResourceType.Gold
            : Enum.TryParse<ResourceType>(type.Resource, out var resource) ? resource : null;
        _baseIncomePerTick = type.Kind == BuildingKind.Production ? type.ProductionRate
            : type.Kind == BuildingKind.Residential ? type.IncomePerTick : 0;
        Level = 1;
        Status = BuildingStatus.UnderConstruction;
    }

    public Guid Id { get; private set; }
    public int TypeId { get; }
    public string Name { get; }
    public int Level { get; private set; }
    public int MaxLevel { get; }
    public int BuildCost { get; }
    public int UpgradeCost { get; }
    public ResourceAmounts BuildResources { get; }
    public ResourceAmounts UpgradeResources { get; }
    public ResourceAmounts InvestedResources { get; private set; } = new();
    public ResourceAmounts DemolitionResources => InvestedResources.Half();
    public int DemolitionRefund => DemolitionResources.Gold;
    public ResourceType? ProducedResource { get; }
    public ResourceAmounts OutputPerTick => ProducedResource switch
    {
        ResourceType.Gold => new(CollectIncome()),
        ResourceType.Wood => new(wood: CollectIncome()),
        ResourceType.Stone => new(stone: CollectIncome()),
        _ => new()
    };
    public BuildingStatus Status { get; private set; }
    public abstract BuildingKind Kind { get; }

    /// <summary>Дохід за один тік з урахуванням рівня.</summary>
    public int IncomePerTick => _baseIncomePerTick * Level;

    /// <summary>Чи доступне покращення будівлі за рівнем і статусом.</summary>
    public bool CanUpgrade => Status == BuildingStatus.Active && Level < MaxLevel;

    /// <summary>Завершує будівництво. Етап 5: будівництво миттєве (UnderConstruction → Active).</summary>
    public void Build(bool isFree = false)
    {
        if (Status != BuildingStatus.UnderConstruction)
            throw new InvalidOperationException("Будівля вже збудована.");
        InvestedResources = isFree ? new() : BuildResources;
        Status = MaxLevel == 1 ? BuildingStatus.MaxLevel : BuildingStatus.Active;
    }

    /// <summary>
    /// Покращує будівлю: Active → Upgrading → Active / MaxLevel.
    /// Повертає false, якщо будівля вже має максимальний рівень або неактивна.
    /// </summary>
    public virtual bool Upgrade()
    {
        if (Status != BuildingStatus.Active || Level >= MaxLevel)
            return false;

        Status = BuildingStatus.Upgrading;
        InvestedResources = InvestedResources.Add(UpgradeResources);
        Level++;
        Status = Level >= MaxLevel ? BuildingStatus.MaxLevel : BuildingStatus.Active;
        return true;
    }

    /// <summary>Дохід за тік: лише для активних будівель.</summary>
    public int CollectIncome() =>
        Status is BuildingStatus.Active or BuildingStatus.MaxLevel ? IncomePerTick : 0;

    /// <summary>Відновлює стан будівлі із збереження.</summary>
    public void Restore(Guid id, int level, BuildingStatus status, ResourceAmounts? investedResources = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        Id = id;
        Level = Math.Min(level, MaxLevel);
        Status = status;
        InvestedResources = investedResources ?? BuildResources.Add(UpgradeResources.Multiply(Level - 1));
    }
}
