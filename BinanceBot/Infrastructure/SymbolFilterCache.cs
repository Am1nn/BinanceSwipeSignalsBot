namespace Bot.Infrastructure;

using Binance.Net.Clients;
using System.Linq;
using Bot.Config;
using Bot.Utils;

public sealed class SymbolFilterCache
{
    public decimal PriceTickSize { get; private set; } = 0.01m;
    public decimal QtyStepSize { get; private set; } = 0.001m;
    public int PriceScale { get; private set; } = 2;

    public string PriceFmt(decimal v) => v.ToString($"F{PriceScale}", System.Globalization.CultureInfo.InvariantCulture);

    public async Task Refresh(BinanceRestClient rest)
    {
        var exInfo = await rest.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
        if (!exInfo.Success) return;
        var sym = exInfo.Data.Symbols.FirstOrDefault(s => s.Name == AppConfig.Symbol);
        if (sym == null) return;

        PriceTickSize = sym.PriceFilter?.TickSize ?? 0.01m;
        QtyStepSize = sym.LotSizeFilter?.StepSize ?? 0.001m;
        PriceScale = Math.Max(0, MathUtils.CountDecimals(PriceTickSize));
    }
}
