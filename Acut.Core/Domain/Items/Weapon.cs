namespace Acut.Core.Domain.Items;

public sealed class Weapon : Item
{
    public Weapon(int id, string name, string description, int price, int bonusAttack, int level = 1)
        : base(id, name, description, price, level)
    {
        BonusAttack = bonusAttack;
    }

    public int BonusAttack { get; }
}
