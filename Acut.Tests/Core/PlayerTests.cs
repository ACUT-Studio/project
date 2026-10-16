using Acut.Core.Domain;
using Acut.Core.Domain.Characters;
using Xunit;

namespace Acut.Tests.Core;

public class PlayerTests
{
    private static Player CreatePlayer(int gold = 0) =>
        new("Тест", AppearanceSettings.Default, gold: gold);

    [Fact]
    public void SpendGold_NotEnough_ReturnsFalseAndKeepsBalance()
    {
        var player = CreatePlayer(gold: 50);

        var result = player.SpendGold(100);

        Assert.False(result);
        Assert.Equal(50, player.Gold);
    }

    [Fact]
    public void SpendGold_Enough_DeductsAmount()
    {
        var player = CreatePlayer(gold: 150);

        Assert.True(player.SpendGold(100));
        Assert.Equal(50, player.Gold);
    }

    [Fact]
    public void TakeDamage_BeyondHealth_StopsAtZero()
    {
        var player = CreatePlayer();

        player.TakeDamage(player.MaxHealth + 500);

        Assert.Equal(0, player.Health);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public void Heal_BeyondMax_StopsAtMaxHealth()
    {
        var player = CreatePlayer();
        player.TakeDamage(10);

        player.Heal(1000);

        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Fact]
    public void GainExperience_EnoughForLevel_LevelsUpAndGivesSkillPoint()
    {
        var player = CreatePlayer();

        player.GainExperience(GameConfig.ExperiencePerLevel);

        Assert.Equal(2, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(1, player.SkillPoints);
        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player("  ", AppearanceSettings.Default));
    }
}
