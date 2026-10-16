namespace Acut.Core.Domain.Items;

public sealed class Armor : Item
{
    public Armor(int id, string name, string description, int price, int bonusDefense, int level = 1)
        : base(id, name, description, price, level)
    {
        BonusDefense = bonusDefense;
    }

    public int BonusDefense { get; }
}
