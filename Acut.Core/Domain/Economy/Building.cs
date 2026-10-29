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
        _baseIncomePerTick = type.IncomePerTick;
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
    public BuildingStatus Status { get; private set; }
    public abstract BuildingKind Kind { get; }

    /// <summary>Дохід за один тік з урахуванням рівня.</summary>
    public int IncomePerTick => _baseIncomePerTick * Level;

    /// <summary>Чи доступне покращення будівлі за рівнем і статусом.</summary>
    public bool CanUpgrade => Status == BuildingStatus.Active && Level < MaxLevel;

    /// <summary>Завершує будівництво. Етап 5: будівництво миттєве (UnderConstruction → Active).</summary>
    public void Build()
    {
        if (Status != BuildingStatus.UnderConstruction)
            throw new InvalidOperationException("Будівля вже збудована.");
        Status = BuildingStatus.Active;
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
        Level++;
        Status = Level >= MaxLevel ? BuildingStatus.MaxLevel : BuildingStatus.Active;
        return true;
    }

    /// <summary>Дохід за тік: лише для активних будівель.</summary>
    public int CollectIncome() =>
        Status is BuildingStatus.Active or BuildingStatus.MaxLevel ? IncomePerTick : 0;

    /// <summary>Відновлює стан будівлі із збереження.</summary>
    public void Restore(Guid id, int level, BuildingStatus status)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        Id = id;
        Level = Math.Min(level, MaxLevel);
        Status = status;
    }
}
