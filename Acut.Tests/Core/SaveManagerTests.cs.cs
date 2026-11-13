using Acut.Core.Abstractions;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public sealed class SaveManagerTests
{
    [Fact]
    public void SaveManager_RegisterSaveLoadAndDelete_ManagesCurrentSession()
    {
        var repo = new InMemorySaveRepository();
        var manager = new SaveManager(repo);
        var session = GameSession.StartNewGame("Марко", null, new FakeCatalog());

        // До реєстрації у SaveManager виклик session.Save() кидає виняток
        Assert.Throws<InvalidOperationException>(() => session.Save());

        manager.Register(session);
        Assert.Same(session, manager.Current);

        session.Save();
        Assert.Single(manager.ListSaves());

        var loaded = manager.LoadGame(session.SessionId);
        Assert.NotNull(loaded);
        Assert.Same(loaded, manager.Current);
        Assert.Null(manager.LoadGame(Guid.NewGuid()));

        manager.DeleteSave(session.SessionId);
        Assert.Null(manager.Current);
        Assert.Empty(manager.ListSaves());
    }

    [Fact]
    public void AutoSave_SavesOnlyWhenSessionIsInPlayableState()
    {
        var repo = new InMemorySaveRepository();
        var manager = new SaveManager(repo);

        // Без активної сесії AutoSave повертає false
        Assert.False(manager.AutoSave());

        var session = GameSession.StartNewGame("Марко", null, new FakeCatalog());
        manager.Register(session);

        // У стані City автозбереження спрацьовує
        Assert.True(manager.AutoSave());
        Assert.Single(repo.ListSaves());

        // У стані MainMenu автозбереження не виконується
        session.ChangeState(GameState.Paused);
        session.ChangeState(GameState.MainMenu);
        Assert.False(manager.AutoSave());
    }

    private sealed class InMemorySaveRepository : ISaveRepository
    {
        private readonly Dictionary<Guid, GameSession> _saves = new();

        public void Save(GameSession session) => _saves[session.SessionId] = session;
        public GameSession? Load(Guid saveId) => _saves.GetValueOrDefault(saveId);
        public IReadOnlyList<SaveSlotInfo> ListSaves() =>
            _saves.Values.Select(s => new SaveSlotInfo(s.SessionId, s.Player.Name, DateTime.UtcNow)).ToList();
        public void Delete(Guid saveId) => _saves.Remove(saveId);
    }
}