using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Economy;

public sealed class StockMarket
{
    public StockMarket(IEnumerable<Stock>? stocks = null)
    {
        Stocks = stocks?.ToList() ?? new List<Stock>();
    }

    public List<Stock> Stocks { get; }

    public bool Buy(Stock stock, int qty, Player player)
    {
        // TODO(Етап 6): списати qty * Price, збільшити Player.StockHoldings.
        throw new NotImplementedException("Етап 6: StockMarket.Buy");
    }

    public bool Sell(Stock stock, int qty, Player player)
    {
        // TODO(Етап 6): зменшити Player.StockHoldings, нарахувати qty * Price.
        throw new NotImplementedException("Етап 6: StockMarket.Sell");
    }

    public void UpdatePrices()
    {
        // TODO(Етап 6): викликати Fluctuate() для кожної акції.
        throw new NotImplementedException("Етап 6: StockMarket.UpdatePrices");
    }
}
