namespace Bot.Domain;

public sealed class TradingStats
{
    public int TradesTotal { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal CalcWinRate() => TradesTotal <= 0 ? 0m : (decimal)Wins / TradesTotal * 100m;
}

public sealed class SessionState
{
    public SideDir CurrentSide { get; set; } = SideDir.None;
    public decimal? EntryPrice { get; set; }
    public decimal? EntryQty { get; set; }
    public DateTime? LastOpenTimeUtc { get; set; }
    public decimal? LastPlannedTpPrice { get; set; }
    public DateTime? LastTradeKlineCloseTime { get; set; }
    public void Reset()
    {
        CurrentSide = SideDir.None;
        EntryPrice = null;
        EntryQty = null;
        LastOpenTimeUtc = null;
        LastPlannedTpPrice = null;
    }
}
