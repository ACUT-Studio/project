namespace Acut.Core.Domain.Economy;

public sealed class MarketBuilding : Building
{
    public MarketBuilding(BuildingType type) : base(type) { }

    public override BuildingKind Kind => BuildingKind.Market;

    /// <summary>Біржа міста. Призначається містом при додаванні будівлі.</summary>
    public StockMarket? Market { get; internal set; }
}
