namespace Bot.Utils;

public static class MathUtils
{
    public static decimal Clamp(decimal v, decimal mn, decimal mx) => v < mn ? mn : (v > mx ? mx : v);
    public static int Clamp(int v, int mn, int mx) => v < mn ? mn : (v > mx ? mx : v);

    public static int CountDecimals(decimal d)
    {
        d = Math.Abs(d);
        int decimals = 0;
        while (d != Math.Floor(d))
        {
            d *= 10;
            decimals++;
            if (decimals > 12) break;
        }
        return decimals;
    }
    public static decimal RoundToStep(decimal value, decimal step)
    {
        if (step <= 0) return value;
        var factor = value / step;
        var rounded = decimal.Round(factor, 0, MidpointRounding.AwayFromZero) * step;
        return decimal.Round(rounded, CountDecimals(step), MidpointRounding.AwayFromZero);
    }
    public static decimal RoundDownToStep(decimal value, decimal step)
    {
        if (step <= 0) return value;
        var x = Math.Floor(value / step) * step;
        return decimal.Round(x, CountDecimals(step), MidpointRounding.AwayFromZero);
    }
}
