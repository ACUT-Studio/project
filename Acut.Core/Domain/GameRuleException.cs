namespace Acut.Core.Domain;

/// <summary>
/// Порушення правил гри (не вистачає валюти, будівля заблокована, некоректне ім'я тощо).
/// ViewModel перехоплює її й показує гравцю повідомлення.
/// </summary>
public sealed class GameRuleException : Exception
{
    public GameRuleException(string message) : base(message) { }
}
