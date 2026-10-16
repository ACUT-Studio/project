namespace Acut.Core.Domain.Combat;

public sealed record ActionResult(BattleAction Action, int Damage, int HealedAmount, string Message);
