using Acut.Core.Domain.Economy;

namespace Acut.Core.Domain.Progress;

public sealed class Skill
{
    public Skill(int id, string name, string description, int cost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Cost = cost;
    }

    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Cost { get; }
    public bool IsUnlocked { get; private set; }
    public List<Skill> Prerequisites { get; } = new();
    public IReadOnlyList<BuildingType> UnlocksBuildings { get; set; } = Array.Empty<BuildingType>();

    internal void MarkUnlocked() => IsUnlocked = true;
}
