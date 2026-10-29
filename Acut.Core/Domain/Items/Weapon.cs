namespace Acut.Core.Domain.Items;

public sealed class Weapon : Item
{
    private readonly int _baseBonusAttack;

    public Weapon(int id, string name, string description, int price, int bonusAttack, int level = 1)
        : base(id, name, description, price, level)
    {
        _baseBonusAttack = bonusAttack;
        BonusAttack = bonusAttack * level;
    }

    public int BonusAttack { get; private set; }

    protected override void OnLevelChanged() => BonusAttack = _baseBonusAttack * Level;

    public override Item Clone() => new Weapon(Id, Name, Description, Price, _baseBonusAttack, Level);
}
