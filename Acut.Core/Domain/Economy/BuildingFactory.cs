namespace Acut.Core.Domain.Economy;

/// <summary>Створює потрібний підклас Building за полем Kind каталогу.</summary>
public static class BuildingFactory
{
    public static Building Create(BuildingType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.Kind switch
        {
            BuildingKind.Residential => new ResidentialBuilding(type),
            BuildingKind.Production => new ProductionBuilding(type),
            BuildingKind.Workshop => new WorkshopBuilding(type),
            BuildingKind.Market => new MarketBuilding(type),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type.Kind, "Невідомий тип будівлі.")
        };
    }
}
