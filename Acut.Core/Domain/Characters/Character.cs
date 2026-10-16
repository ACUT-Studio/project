namespace Acut.Core.Domain.Characters;

public abstract class Character
{
    protected Character(string name, int maxHealth, int health, int attackPower, int defense)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ім'я не може бути порожнім.", nameof(name));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHealth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(attackPower);
        ArgumentOutOfRangeException.ThrowIfNegative(defense);

        Name = name.Trim();
        MaxHealth = maxHealth;
        Health = Math.Clamp(health, 0, maxHealth);
        AttackPower = attackPower;
        Defense = defense;
    }

    public string Name { get; }
    public int Health { get; protected set; }
    public int MaxHealth { get; protected set; }
    public int AttackPower { get; protected set; }
    public int Defense { get; protected set; }
    public bool IsAlive => Health > 0;

    /// <summary>Віднімає здоров'я. Здоров'я не опускається нижче 0.</summary>
    public void TakeDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Health = Math.Max(0, Health - amount);
    }

    /// <summary>Відновлює здоров'я. Здоров'я не перевищує MaxHealth.</summary>
    public void Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Health = Math.Min(MaxHealth, Health + amount);
    }
}
