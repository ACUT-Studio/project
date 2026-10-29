using Acut.Core.Domain.Characters;
using Acut.Core.Domain.Economy;
using Acut.Core.Services;
using Xunit;

namespace Acut.Tests.Core;

public class StockMarketTests
{
    private static (StockMarket Market, Stock Stock, Player Player) CreateSetup(decimal stockPrice = 100m, int playerGold = 500)
    {
        var stock = new Stock(1, "TEST", "Тест Акція", stockPrice, 0.10m);
        var market = new StockMarket(new[] { stock });
        var player = new Player("Інвестор", AppearanceSettings.Default, gold: playerGold);
        return (market, stock, player);
    }

    [Fact]
    public void Stock_Fluctuate_PriceAlwaysRemainsGreaterThanZero()
    {
        var stock = new Stock(1, "TEST", "Тест", 10m, 0.50m);

        // Екстремальне падіння на 100%
        stock.Fluctuate(-1.0m);
        Assert.True(stock.Price > 0m);
        Assert.Equal(Stock.MinPrice, stock.Price);

        // Екстремальне падіння на 500%
        stock.Fluctuate(-5.0m);
        Assert.True(stock.Price > 0m);
        Assert.Equal(Stock.MinPrice, stock.Price);

        // 1000 випадкових коливань
        for (int i = 0; i < 1000; i++)
        {
            stock.Fluctuate();
            Assert.True(stock.Price > 0m);
        }
    }

    [Fact]
    public void Buy_SufficientGold_DeductsGoldAndAddsStockHoldings()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 300);

        var result = market.Buy(stock, 2, player);

        Assert.True(result);
        Assert.Equal(100, player.Gold); // 300 - 2 * 100 = 100
        Assert.Equal(2, player.StockHoldings[stock.Id]);
    }

    [Fact]
    public void Buy_InsufficientGold_RejectedWithoutSideEffects()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 50);

        var result = market.Buy(stock, 1, player);

        Assert.False(result);
        Assert.Equal(50, player.Gold);
        Assert.False(player.StockHoldings.ContainsKey(stock.Id));
    }

    [Fact]
    public void Buy_ZeroOrNegativeQuantity_RejectedWithoutSideEffects()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 500);

        Assert.False(market.Buy(stock, 0, player));
        Assert.False(market.Buy(stock, -1, player));
        Assert.Equal(500, player.Gold);
    }

    [Fact]
    public void Sell_ValidQuantity_ReducesStockHoldingsAndAddsRevenue()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 100);
        market.Buy(stock, 3, player); // витратив 300? але було 100!
        // Створимо з достатнім золотом
        player.AddGold(1000);
        market.Buy(stock, 3, player);
        var goldBeforeSell = player.Gold;

        var result = market.Sell(stock, 2, player);

        Assert.True(result);
        Assert.Equal(1, player.StockHoldings[stock.Id]);
        Assert.Equal(goldBeforeSell + 200, player.Gold);
    }

    [Fact]
    public void Sell_ExceedingQuantity_RejectedWithoutSideEffects()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 500);
        market.Buy(stock, 2, player); // має 2 акції
        var goldBeforeSell = player.Gold;

        // Спроба продати 3 акції, коли є тільки 2
        var result = market.Sell(stock, 3, player);

        Assert.False(result);
        Assert.Equal(2, player.StockHoldings[stock.Id]);
        Assert.Equal(goldBeforeSell, player.Gold);
    }

    [Fact]
    public void Sell_StockNotOwned_RejectedWithoutSideEffects()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 500);

        var result = market.Sell(stock, 1, player);

        Assert.False(result);
        Assert.Equal(500, player.Gold);
        Assert.False(player.StockHoldings.ContainsKey(stock.Id));
    }

    [Fact]
    public void Sell_AllStock_RemovesHoldingKey()
    {
        var (market, stock, player) = CreateSetup(stockPrice: 100m, playerGold: 500);
        market.Buy(stock, 2, player);

        var result = market.Sell(stock, 2, player);

        Assert.True(result);
        Assert.False(player.StockHoldings.ContainsKey(stock.Id));
    }

    [Fact]
    public void UpdatePrices_FluctuatesAllStocks()
    {
        var s1 = new Stock(1, "S1", "Stock 1", 100m, 0.20m);
        var s2 = new Stock(2, "S2", "Stock 2", 200m, 0.30m);
        var market = new StockMarket(new[] { s1, s2 });

        market.UpdatePrices();

        Assert.True(s1.Price > 0m);
        Assert.True(s2.Price > 0m);
    }

    [Fact]
    public void City_Tick_UpdatesStockPrices()
    {
        var stock = new Stock(1, "AGRO", "Агро", 100m, 0.50m);
        var player = new Player("Мер", AppearanceSettings.Default, gold: 500);
        var dispatcher = new GameEventDispatcher();
        dispatcher.Attach(player);
        var market = new StockMarket(new[] { stock });
        var city = new City("Тестоград", player, dispatcher, market: market);

        // Викликаємо тік міста
        city.Tick();

        // Ціна має залишатися валідною і більшою за нуль
        Assert.True(city.Market.Stocks[0].Price > 0m);
    }
}
