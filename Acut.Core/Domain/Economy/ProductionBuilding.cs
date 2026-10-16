namespace Acut.Core.Domain.Economy;

public sealed class ProductionBuilding : Building
{
    public ProductionBuilding(BuildingType type) : base(type)
    {
        Resource = type.Resource ?? string.Empty;
        ProductionRate = type.ProductionRate;
    }

    public override BuildingKind Kind => BuildingKind.Production;
    public string Resource { get; }
    public int ProductionRate { get; }
}
