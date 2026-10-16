namespace Acut.Core.Abstractions;

/// <summary>Короткий опис збереження для списку «Продовжити».</summary>
public sealed record SaveSlotInfo(Guid SaveId, string Name, DateTime UpdatedAt);
