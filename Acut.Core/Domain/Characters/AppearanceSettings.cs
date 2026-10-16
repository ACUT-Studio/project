namespace Acut.Core.Domain.Characters;

public sealed record AppearanceSettings(string HairStyle, string SkinColor, string Outfit)
{
    public static AppearanceSettings Default { get; } = new("Short", "Light", "Traveler");
}
