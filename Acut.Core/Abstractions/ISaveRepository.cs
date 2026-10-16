using Acut.Core.Services;

namespace Acut.Core.Abstractions;

/// <summary>Збереження та завантаження стану гри. Запис виконується в одній транзакції.</summary>
public interface ISaveRepository
{
    void Save(GameSession session);
    GameSession? Load(Guid saveId);
    IReadOnlyList<SaveSlotInfo> ListSaves();
    void Delete(Guid saveId);
}
