using Acut.Core.Abstractions;

namespace Acut.Core.Services;

/// <summary>Збереження й завантаження сесії через ISaveRepository, автозбереження.</summary>
public sealed class SaveManager
{
    private readonly ISaveRepository _repository;

    public SaveManager(ISaveRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Поточна активна сесія (її зберігає AutoSave).</summary>
    public GameSession? Current { get; private set; }

    public void Register(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
        session.AttachSaveManager(this);
    }

    public void SaveGame(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _repository.Save(session);
    }

    /// <summary>Завантажує збереження й робить його поточною сесією. Null, якщо збереження не знайдено.</summary>
    public GameSession? LoadGame(Guid saveId)
    {
        var session = _repository.Load(saveId);
        if (session is not null) Register(session);
        return session;
    }

    /// <summary>Зберігає поточну сесію, якщо гра вже розпочата (не меню й не створення персонажа).</summary>
    public bool AutoSave()
    {
        if (Current is null || Current.CurrentState is GameState.MainMenu or GameState.CharacterCreation)
            return false;
        SaveGame(Current);
        return true;
    }

    /// <summary>Збереження від найновішого до найстарішого.</summary>
    public IReadOnlyList<SaveSlotInfo> ListSaves() => _repository.ListSaves();

    public void DeleteSave(Guid saveId)
    {
        _repository.Delete(saveId);
        if (Current?.SessionId == saveId) Current = null;
    }
}
