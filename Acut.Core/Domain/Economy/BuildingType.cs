namespace Acut.Core.Domain.Economy;

/// <summary>Рядок каталогу будівель. BuildCost та UpgradeCost — золоті складові вартості.</summary>
public sealed record BuildingType(
    int Id, BuildingKind Kind, string Name, int BuildCost, int UpgradeCost, int MaxLevel,
    int IncomePerTick, int Capacity, string? Resource, int ProductionRate, int? RequiredSkillId,
    int BuildWoodCost = 0, int BuildStoneCost = 0, int UpgradeWoodCost = 0, int UpgradeStoneCost = 0)
{
    public ResourceAmounts BuildResources => new(BuildCost, BuildWoodCost, BuildStoneCost);
    public ResourceAmounts UpgradeResources => new(UpgradeCost, UpgradeWoodCost, UpgradeStoneCost);
}
