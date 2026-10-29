using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Progress;

namespace Acut.Core.Services;

/// <summary>Єдине джерело тригерів: розсилає GameEvent завданням, досягненням і статистиці.</summary>
public sealed class GameEventDispatcher
{
    private readonly List<Action<GameEvent>> _subscribers = new();

    public void Subscribe(Action<GameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers.Add(handler);
    }

    public void Publish(GameEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        foreach (var handler in _subscribers.ToArray())
            handler(e);
    }

    /// <summary>
    /// Підписує прогрес гравця. Порядок важливий: спершу статистика,
    /// бо досягнення перевіряються за накопиченими лічильниками.
    /// </summary>
    public void Attach(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.SkillTree.Dispatcher = this;
        player.QuestLog.Player = player;
        Subscribe(player.Statistics.Record);
        Subscribe(player.QuestLog.UpdateProgress);
        Subscribe(player.Achievements.CheckTriggers);
    }
}
