namespace Acut.Core.Domain.Economy;

public sealed class ResidentialBuilding : Building
{
    public ResidentialBuilding(BuildingType type) : base(type)
    {
        Capacity = type.Capacity;
    }

    public override BuildingKind Kind => BuildingKind.Residential;

    /// <summary>Скільки мешканців вміщує будівля на 1 рівні.</summary>
    public int Capacity { get; }
}
