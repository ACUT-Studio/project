namespace Acut.Core.Domain.Items;

/// <summary>Предмет каталогу. Id — ItemId із таблиці ItemDefinition.</summary>
public abstract class Item : IEquatable<Item>
{
    protected Item(int id, string name, string description, int price, int level = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва предмета не може бути порожньою.", nameof(name));
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Price = price;
        Level = level;
    }

    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Price { get; }
    public int Level { get; protected set; }
    public virtual int MaxLevel => GameConfig.MaxItemLevel;
    public virtual int UpgradeCost => Price * Level;
    public bool CanUpgrade => Level < MaxLevel;

    /// <summary>Відновлює рівень предмета із збереження.</summary>
    public void RestoreLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        Level = level;
        OnLevelChanged();
    }

    public virtual bool Upgrade()
    {
        if (!CanUpgrade) return false;
        Level++;
        OnLevelChanged();
        return true;
    }

    protected virtual void OnLevelChanged() { }

    public abstract Item Clone();

    public bool Equals(Item? other) => other is not null && other.Id == Id;
    public override bool Equals(object? obj) => Equals(obj as Item);
    public override int GetHashCode() => Id;
}
