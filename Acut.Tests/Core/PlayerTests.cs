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
        // Arrange
        var player = CreatePlayer(gold: 50);

        // Act
        var result = player.SpendGold(100);

        // Assert
        Assert.False(result);
        Assert.Equal(50, player.Gold);
    }

    [Fact]
    public void SpendGold_Enough_DeductsAmount()
    {
        // Arrange
        var player = CreatePlayer(gold: 150);

        // Act
        var result = player.SpendGold(100);

        // Assert
        Assert.True(result);
        Assert.Equal(50, player.Gold);
    }

    [Fact]
    public void TakeDamage_BeyondHealth_StopsAtZero()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.TakeDamage(player.MaxHealth + 500);

        // Assert
        Assert.Equal(0, player.Health);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public void Heal_BeyondMax_StopsAtMaxHealth()
    {
        // Arrange
        var player = CreatePlayer();
        player.TakeDamage(10);

        // Act
        player.Heal(1000);

        // Assert
        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Fact]
    public void GainExperience_EnoughForLevel_LevelsUpAndGivesSkillPoint()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.GainExperience(GameConfig.ExperiencePerLevel);

        // Assert
        Assert.Equal(2, player.Level);
        Assert.Equal(0, player.Experience);
        Assert.Equal(1, player.SkillPoints);
        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Player("  ", AppearanceSettings.Default));
    }

    [Fact]
    public void SpendGold_ExactBalance_ReturnsTrueAndLeavesZeroGold()
    {
        // Arrange
        var player = CreatePlayer(gold: 100);

        // Act
        var result = player.SpendGold(100);

        // Assert
        Assert.True(result);
        Assert.Equal(0, player.Gold);
    }

    [Fact]
    public void TakeDamage_PartialDamage_ReducesHealthWithoutKilling()
    {
        // Arrange
        var player = CreatePlayer();
        var initialHealth = player.Health;

        // Act
        player.TakeDamage(30);

        // Assert
        Assert.Equal(initialHealth - 30, player.Health);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public void Heal_PartialHeal_RestoresHealthCorrectly()
    {
        // Arrange
        var player = CreatePlayer();
        player.TakeDamage(50);
        var healthAfterDamage = player.Health;

        // Act
        player.Heal(20);

        // Assert
        Assert.Equal(healthAfterDamage + 20, player.Health);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public void GainExperience_NotEnoughForLevel_IncreasesExperienceOnly()
    {
        // Arrange
        var player = CreatePlayer();
        var partialExp = GameConfig.ExperiencePerLevel / 2;

        // Act
        player.GainExperience(partialExp);

        // Assert
        Assert.Equal(1, player.Level);
        Assert.Equal(partialExp, player.Experience);
        Assert.Equal(0, player.SkillPoints);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void Constructor_NullOrWhitespaceName_ThrowsArgumentException(string invalidName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Player(invalidName, AppearanceSettings.Default));
    }

    [Fact]
    public void GainGold_ValidAmount_IncreasesPlayerGold()
    {
        // Arrange
        var player = CreatePlayer(gold: 50);

        
        player.AddGold(150);

        // Assert
        Assert.Equal(200, player.Gold);
    }

}