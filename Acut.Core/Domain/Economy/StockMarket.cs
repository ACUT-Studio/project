using Acut.Core.Domain.Characters;

namespace Acut.Core.Domain.Economy;

public sealed class StockMarket
{
    public StockMarket(IEnumerable<Stock>? stocks = null)
    {
        Stocks = stocks?.ToList() ?? new List<Stock>();
    }

    public List<Stock> Stocks { get; }

    /// <summary>
    /// Купує задану кількість акцій: списує qty * Price у гравця, додає до StockHoldings.
    /// Відхиляється без побічних ефектів, якщо не вистачає золота або qty <= 0.
    /// </summary>
    public bool Buy(Stock stock, int qty, Player player)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(player);
        if (qty <= 0) return false;

        var marketStock = Stocks.FirstOrDefault(s => s.Id == stock.Id) ?? stock;
        var totalCost = (int)Math.Round(marketStock.Price * qty, MidpointRounding.AwayFromZero);
        if (totalCost <= 0) totalCost = 1;

        if (player.Gold < totalCost)
            return false;

        if (!player.SpendGold(totalCost))
            return false;

        player.StockHoldings.TryGetValue(marketStock.Id, out var currentQty);
        player.StockHoldings[marketStock.Id] = currentQty + qty;
        return true;
    }

    /// <summary>
    /// Продає задану кількість акцій: зменшує StockHoldings, нараховує виручку qty * Price.
    /// Продаж понад наявне відхиляється без побічних ефектів.
    /// </summary>
    public bool Sell(Stock stock, int qty, Player player)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(player);
        if (qty <= 0) return false;

        var marketStock = Stocks.FirstOrDefault(s => s.Id == stock.Id) ?? stock;
        player.StockHoldings.TryGetValue(marketStock.Id, out var currentQty);
        if (currentQty < qty)
            return false;

        player.StockHoldings[marketStock.Id] = currentQty - qty;
        if (player.StockHoldings[marketStock.Id] == 0)
            player.StockHoldings.Remove(marketStock.Id);

        var revenue = (int)Math.Round(marketStock.Price * qty, MidpointRounding.AwayFromZero);
        player.AddGold(revenue);
        return true;
    }

    /// <summary>Оновлює ціни всіх акцій на ринку (викликає Fluctuate).</summary>
    public void UpdatePrices()
    {
        foreach (var stock in Stocks)
        {
            stock.Fluctuate();
        }
    }
}
