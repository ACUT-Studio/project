namespace Acut.Core.Domain.Economy;

/// <summary>
/// Життєвий цикл будівлі. Locked і Available не зберігаються: вони обчислюються за навичками гравця.
/// </summary>
public enum BuildingStatus
{
    Locked,
    Available,
    UnderConstruction,
    Active,
    Upgrading,
    MaxLevel
}
