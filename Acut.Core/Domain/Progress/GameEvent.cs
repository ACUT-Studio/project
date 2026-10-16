namespace Acut.Core.Domain.Progress;

/// <summary>Коротка подія в пам'яті. Не зберігається в БД.</summary>
public sealed record GameEvent(GameEventType Type, int Value = 1)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
