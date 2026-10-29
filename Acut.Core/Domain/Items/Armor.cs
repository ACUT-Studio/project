namespace Acut.Core.Domain.Items;

public sealed class Armor : Item
{
    private readonly int _baseBonusDefense;

    public Armor(int id, string name, string description, int price, int bonusDefense, int level = 1)
        : base(id, name, description, price, level)
    {
        _baseBonusDefense = bonusDefense;
        BonusDefense = bonusDefense * level;
    }

    public int BonusDefense { get; private set; }

    protected override void OnLevelChanged() => BonusDefense = _baseBonusDefense * Level;

    public override Item Clone() => new Armor(Id, Name, Description, Price, _baseBonusDefense, Level);
}
