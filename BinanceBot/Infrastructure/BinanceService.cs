namespace Bot.Infrastructure;

using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Authentication;
using Bot.Config;
using Bot.Utils;

public sealed class BinanceService
{
    public BinanceRestClient Rest { get; }
    public SymbolFilterCache Filters { get; } = new();
    public BinanceService()
    {
        Rest = new BinanceRestClient();
        Rest.SetApiCredentials(new ApiCredentials(AppConfig.ApiKey, AppConfig.ApiSecret));
    }

    public async Task InitAsync()
    {
        await Filters.Refresh(Rest);
        await SafeSetMarginAndLeverage();
    }

    public async Task SafeSetMarginAndLeverage()
    {
        var acc = Rest.UsdFuturesApi.Account;

        // Position mode → One-way
        try
        {
            var getPm = acc.GetType().GetMethod("GetPositionModeAsync");
            bool? isDual = null;
            if (getPm != null)
            {
                dynamic pm = await (dynamic)getPm.Invoke(acc, null);
                if (pm.Success == true) isDual = (bool)pm.Data.DualSidePosition;
            }
            var setPm = acc.GetType().GetMethod("ChangePositionModeAsync") ?? acc.GetType().GetMethod("SetPositionModeAsync");
            if (setPm != null && isDual != false)
                await (Task)setPm.Invoke(acc, new object?[] { false }); // one-way
        }
        catch { }

        try { await acc.ChangeMarginTypeAsync(AppConfig.Symbol, FuturesMarginType.Isolated); } catch { }
        await acc.ChangeInitialLeverageAsync(AppConfig.Symbol, AppConfig.Leverage);
    }

    public async Task<IReadOnlyList<IBinanceKline>?> GetRecentKlines(int limit, KlineInterval interval)
    {
        var res = await Rest.UsdFuturesApi.ExchangeData.GetKlinesAsync(AppConfig.Symbol, interval, limit: limit);
        return res.Success ? res.Data : null;
    }

    public async Task<decimal?> GetMarkPrice()
    {
        var res = await Rest.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(AppConfig.Symbol);
        return res.Success ? res.Data.MarkPrice : null;
    }

    public async Task CancelReduceOnlyIfNoPosition()
    {
        var open = await Rest.UsdFuturesApi.Trading.GetOpenOrdersAsync(AppConfig.Symbol);
        if (!open.Success) return;

        var posInfo = await Rest.UsdFuturesApi.Account.GetPositionInformationAsync(AppConfig.Symbol);
        var qty = posInfo.Success ? (posInfo.Data.FirstOrDefault()?.Quantity ?? 0m) : 0m;

        if (qty == 0m)
        {
            foreach (var o in open.Data.Where(o => o.ReduceOnly == true))
            {
                try { await Rest.UsdFuturesApi.Trading.CancelOrderAsync(AppConfig.Symbol, orderId: o.Id); } catch { }
            }
        }
    }

    public async Task<(decimal qty, decimal step, decimal minNotional)> ComputeOrderQty(decimal markPrice, decimal availableUsdt)
    {
        var exInfo = await Rest.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
        if (!exInfo.Success) throw new Exception($"ExchangeInfo error: {exInfo.Error}");

        var sym = exInfo.Data.Symbols.First(s => s.Name == AppConfig.Symbol);
        var step = sym.LotSizeFilter?.StepSize ?? 0.001m;
        var minNotional = sym.MinNotionalFilter?.MinNotional ?? 5m;

        var baseMargin = availableUsdt * AppConfig.MarginUseRatio;
        var positionNotional = Math.Max(baseMargin * AppConfig.Leverage, minNotional);

        var rawQty = positionNotional / markPrice;
        var qty = MathUtils.RoundDownToStep(rawQty, step);
        if (qty <= 0) throw new Exception("Qty 0; min notional/step filters.");

        return (qty, step, minNotional);
    }
}
