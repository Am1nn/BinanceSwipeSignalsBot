namespace Bot.Trading;

public static class EmaCalculator
{
    public static List<decimal> Ema(List<decimal> prices, int period)
    {
        if (period <= 1) return prices.ToList();
        var k = 2m / (period + 1);
        var ema = new List<decimal>(prices.Count);
        decimal prev = prices.Take(period).Average();
        for (int i = 0; i < prices.Count; i++)
        {
            if (i < period) ema.Add(prices[i]);
            else
            {
                var current = prices[i] * k + prev * (1 - k);
                ema.Add(current);
                prev = current;
            }
        }
        return ema;
    }
}
