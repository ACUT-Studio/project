using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Items;

public sealed class Potion : Item
{
    public Potion(int id, string name, string description, int price, PotionEffect effect, int effectValue, int level = 1)
        : base(id, name, description, price, level)
    {
        Effect = effect;
        EffectValue = effectValue;
    }

    public PotionEffect Effect { get; }
    public int EffectValue { get; }

    public override Item Clone() => new Potion(Id, Name, Description, Price, Effect, EffectValue, Level);

    public void Use(Character target)
    {
        ArgumentNullException.ThrowIfNull(target);
        switch (Effect)
        {
            case PotionEffect.Heal:
                target.Heal(EffectValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Effect), Effect, "Невідомий ефект зілля.");
        }
    }
}
