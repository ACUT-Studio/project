namespace Acut.Core.Domain.Economy;

/// <summary>Невід'ємні кількості трьох рівноцінних ресурсів міста.</summary>
public sealed record ResourceAmounts
{
    public ResourceAmounts(int gold = 0, int wood = 0, int stone = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        ArgumentOutOfRangeException.ThrowIfNegative(wood);
        ArgumentOutOfRangeException.ThrowIfNegative(stone);
        Gold = gold;
        Wood = wood;
        Stone = stone;
    }

    public int Gold { get; }
    public int Wood { get; }
    public int Stone { get; }
    public bool IsEmpty => Gold == 0 && Wood == 0 && Stone == 0;
    public ResourceAmounts Add(ResourceAmounts other) => new(checked(Gold + other.Gold), checked(Wood + other.Wood), checked(Stone + other.Stone));
    public ResourceAmounts Multiply(int count) => new(checked(Gold * count), checked(Wood * count), checked(Stone * count));
    public ResourceAmounts Half() => new(Gold / 2, Wood / 2, Stone / 2);
}
