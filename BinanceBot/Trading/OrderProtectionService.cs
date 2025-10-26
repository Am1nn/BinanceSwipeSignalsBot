namespace Bot.Trading;

using Bot.Domain;
using Bot.Infrastructure;
using Bot.Config;
using Bot.Utils;
using Binance.Net.Enums;

public sealed class OrderProtectionService
{
    private readonly BinanceService _binance;

    public OrderProtectionService(BinanceService binance) => _binance = binance;

    private static decimal SafeStop(decimal target, decimal mark, bool shouldBeAbove, decimal tick, decimal pctBuf = 0.0003m)
    {
        var minGap = Math.Max(tick * 2m, mark * pctBuf);
        decimal safe = target;

        if (shouldBeAbove)
        {
            if (safe <= mark + minGap) safe = mark + minGap;
        }
        else
        {
            if (safe >= mark - minGap) safe = mark - minGap;
        }
        return MathUtils.RoundToStep(safe, tick);
    }

    public async Task<(decimal tpSafe, decimal slSafe)> PlaceProtectiveOrders(SideDir side, decimal qty, decimal refPrice)
    {
        var mark = await _binance.GetMarkPrice() ?? refPrice;
        var tick = _binance.Filters.PriceTickSize;

        var tpRaw = side == SideDir.Long ? refPrice * (1 + AppConfig.TpPct) : refPrice * (1 - AppConfig.TpPct);
        var slRaw = side == SideDir.Long ? refPrice * (1 - AppConfig.SlPct) : refPrice * (1 + AppConfig.SlPct);

        decimal tpSafe, slSafe;
        if (side == SideDir.Long)
        {
            tpSafe = SafeStop(tpRaw, mark, shouldBeAbove: true, tick: tick);
            slSafe = SafeStop(slRaw, mark, shouldBeAbove: false, tick: tick);
        }
        else
        {
            tpSafe = SafeStop(tpRaw, mark, shouldBeAbove: false, tick: tick);
            slSafe = SafeStop(slRaw, mark, shouldBeAbove: true, tick: tick);
        }

        var tpSide = side == SideDir.Long ? OrderSide.Sell : OrderSide.Buy;

        var tpRes = await _binance.Rest.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: AppConfig.Symbol,
            side: tpSide,
            type: FuturesOrderType.TakeProfitMarket,
            quantity: qty,
            stopPrice: tpSafe,
            reduceOnly: true
        );
        if (!tpRes.Success) Console.WriteLine($"[TP WARN] {tpRes.Error}");

        var slRes = await _binance.Rest.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: AppConfig.Symbol,
            side: tpSide,
            type: FuturesOrderType.StopMarket,
            quantity: qty,
            stopPrice: slSafe,
            reduceOnly: true
        );
        if (!slRes.Success) Console.WriteLine($"[SL WARN] {slRes.Error}");

        return (tpSafe, slSafe);
    }

    public async Task EnsureProtectiveOrders(decimal currentPrice, SessionState state)
    {
        try
        {
            var posInfo = await _binance.Rest.UsdFuturesApi.Account.GetPositionInformationAsync(AppConfig.Symbol);
            if (!posInfo.Success) return;

            var p = posInfo.Data.FirstOrDefault();
            if (p == null) return;
            var qty = p.Quantity;
            if (qty == 0m) return;

            var openOrdersRes = await _binance.Rest.UsdFuturesApi.Trading.GetOpenOrdersAsync(AppConfig.Symbol);
            if (!openOrdersRes.Success) return;
            var openReduce = openOrdersRes.Data.Where(o => o.ReduceOnly == true).ToList();

            bool hasTp = openReduce.Any(o => o.Type == FuturesOrderType.TakeProfitMarket);
            bool hasSl = openReduce.Any(o => o.Type == FuturesOrderType.StopMarket);
            if (hasTp && hasSl) return;

            var side = qty > 0 ? SideDir.Long : SideDir.Short;
            var basis = state.EntryPrice ?? currentPrice;

            await PlaceProtectiveOrders(side, Math.Abs(qty), basis);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ENSURE TP/SL] {ex.Message}");
        }
    }
}
