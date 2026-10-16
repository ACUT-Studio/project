namespace Acut.Core.Domain.Economy;

/// <summary>Рядок каталогу будівель (таблиця BuildingType).</summary>
public sealed record BuildingType(
    int Id, BuildingKind Kind, string Name, int BuildCost, int UpgradeCost, int MaxLevel,
    int IncomePerTick, int Capacity, string? Resource, int ProductionRate, int? RequiredSkillId);
