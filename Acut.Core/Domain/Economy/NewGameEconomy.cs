namespace Acut.Core.Domain.Economy;

public enum ResourceType { Gold, Wood, Stone }

/// <summary>Початкові ресурси й безкоштовна будівля задаються каталогом БД.</summary>
public sealed record NewGameEconomy(ResourceAmounts Resources, int? StartingBuildingTypeId = null);
