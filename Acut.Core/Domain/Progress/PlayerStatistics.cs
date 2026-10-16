namespace Acut.Core.Domain.Progress;

public sealed class PlayerStatistics
{
    public int BattlesWon { get; private set; }
    public int BattlesLost { get; private set; }
    public int BuildingsBuilt { get; private set; }
    public int TotalGoldEarned { get; private set; }
    public int PotionsCrafted { get; private set; }
    public TimeSpan PlayTime { get; private set; }

    public void Record(GameEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        switch (e.Type)
        {
            case GameEventType.BattleWon: BattlesWon += e.Value; break;
            case GameEventType.BattleLost: BattlesLost += e.Value; break;
            case GameEventType.BuildingBuilt: BuildingsBuilt += e.Value; break;
            case GameEventType.GoldEarned: TotalGoldEarned += e.Value; break;
            case GameEventType.PotionCrafted: PotionsCrafted += e.Value; break;
        }
    }

    /// <summary>Накопичене значення лічильника для типу події (0, якщо тип не відстежується).</summary>
    public int GetTotal(GameEventType type) => type switch
    {
        GameEventType.BattleWon => BattlesWon,
        GameEventType.BattleLost => BattlesLost,
        GameEventType.BuildingBuilt => BuildingsBuilt,
        GameEventType.GoldEarned => TotalGoldEarned,
        GameEventType.PotionCrafted => PotionsCrafted,
        _ => 0
    };

    public void AddPlayTime(TimeSpan elapsed)
    {
        if (elapsed > TimeSpan.Zero) PlayTime += elapsed;
    }

    public void Restore(int battlesWon, int battlesLost, int buildingsBuilt, int totalGoldEarned, int potionsCrafted, TimeSpan playTime)
    {
        BattlesWon = battlesWon;
        BattlesLost = battlesLost;
        BuildingsBuilt = buildingsBuilt;
        TotalGoldEarned = totalGoldEarned;
        PotionsCrafted = potionsCrafted;
        PlayTime = playTime;
    }
}
