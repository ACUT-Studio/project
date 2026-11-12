using Acut.Core.Domain.Characters;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Data;

public sealed class AppearancePersistenceTests
{
    [Theory]
    [InlineData("Short", "Light", "Traveler")]
    [InlineData("Long", "Tan", "Merchant")]
    [InlineData("Braid", "Dark", "Knight")]
    public void SaveLoad_PreservesSelectedAppearance(string hair, string skin, string outfit)
    {
        using var db = new TestDatabase();
        var expected = new AppearanceSettings(hair, skin, outfit);
        var session = GameSession.StartNewGame("Марко", expected, db.Catalog);
        db.Saves.Save(session);
        var loaded = db.Saves.Load(session.SessionId)!;
        Assert.Equal(expected, loaded.Player.Appearance);
        db.Saves.Save(loaded);
        Assert.Equal(expected, db.Saves.Load(session.SessionId)!.Player.Appearance);
    }
}
