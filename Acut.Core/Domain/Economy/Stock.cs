namespace Acut.Core.Domain.Economy;

public sealed class Stock
{
    public const decimal MinPrice = 0.01m;

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

    /// <summary>Випадкова зміна ціни в межах Volatility. Ціна завжди більша за нуль.</summary>
    public void Fluctuate() => Fluctuate(Random.Shared);

    public void Fluctuate(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var delta = (decimal)(random.NextDouble() * 2.0 - 1.0) * Volatility;
        ApplyPriceDelta(delta);
    }

    /// <summary>Зміна ціни на заданий відсоток (наприклад, для тестування екстремальних умов).</summary>
    public void Fluctuate(decimal percentChange) => ApplyPriceDelta(percentChange);

    private void ApplyPriceDelta(decimal delta)
    {
        var newPrice = Math.Round(Price * (1m + delta), 2, MidpointRounding.AwayFromZero);
        if (newPrice < MinPrice)
            newPrice = MinPrice;
        Price = newPrice;
    }
}
