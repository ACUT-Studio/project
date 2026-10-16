namespace Acut.Core.Domain.Economy;

public sealed class Stock
{
    public Stock(int id, string symbol, string name, decimal basePrice, decimal volatility, decimal? price = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePrice);
        Id = id;
        Symbol = symbol;
        Name = name;
        BasePrice = basePrice;
        Volatility = volatility;
        Price = price ?? basePrice;
    }

    public int Id { get; }
    public string Symbol { get; }
    public string Name { get; }
    public decimal BasePrice { get; }
    public decimal Price { get; private set; }
    public decimal Volatility { get; }

    /// <summary>Відновлює поточну ціну із збереження.</summary>
    public void RestorePrice(decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        Price = price;
    }

    public void Fluctuate()
    {
        // TODO(Етап 6): випадкова зміна ціни в межах Volatility, ціна > 0.
        throw new NotImplementedException("Етап 6: Stock.Fluctuate");
    }
}
