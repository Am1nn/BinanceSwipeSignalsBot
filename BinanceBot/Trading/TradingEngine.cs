namespace Bot.Trading;

using System.Globalization;
using Binance.Net.Enums;
using Bot.Config;
using Bot.Domain;
using Bot.Infrastructure;

public sealed class TradingEngine
{
    private readonly BinanceService _binance;
    private readonly WalletService _wallet;
    private readonly OrderProtectionService _protection;

    private readonly SessionState _state = new();
    private readonly TradingStats _stats = new();

    private volatile bool _enabled = false;
    private CancellationTokenSource? _cts;

    public TradingEngine(BinanceService binance, WalletService wallet, OrderProtectionService protection)
    {
        _binance = binance; _wallet = wallet; _protection = protection;
    }

    public TradingStats Stats => _stats;
    public SessionState State => _state;
    public bool IsEnabled => _enabled;

    public async Task StartAsync()
    {
        try { await _binance.CancelReduceOnlyIfNoPosition(); } catch { }

        await _wallet.Refresh(true);
        await _binance.SafeSetMarginAndLeverage();

        var avail = _wallet.LastAvailableBalance ?? 0m;
        if (avail <= 0m) throw new Exception($"Insufficient available USDT: {avail:0.##}");

        if (_enabled) return;
        _enabled = true;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => Loop(_cts.Token));
    }

    public void Stop()
    {
        if (!_enabled) return;
        _enabled = false;
        _cts?.Cancel();
    }

    public async Task StopSessionAsync()
    {
        var posInfoRes = await _binance.Rest.UsdFuturesApi.Account.GetPositionInformationAsync(AppConfig.Symbol);
        if (!posInfoRes.Success) throw new Exception(posInfoRes.Error.ToString());

        var p = posInfoRes.Data.FirstOrDefault();
        var qty = p?.Quantity ?? 0m;
        if (qty == 0m) return;

        var side = qty > 0 ? OrderSide.Sell : OrderSide.Buy;
        var closeRes = await _binance.Rest.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: AppConfig.Symbol,
            side: side,
            type: FuturesOrderType.Market,
            quantity: Math.Abs(qty),
            reduceOnly: true
        );
        if (!closeRes.Success) throw new Exception(closeRes.Error.ToString());

        var openOrdersRes = await _binance.Rest.UsdFuturesApi.Trading.GetOpenOrdersAsync(AppConfig.Symbol);
        if (openOrdersRes.Success)
        {
            foreach (var o in openOrdersRes.Data.Where(o => o.ReduceOnly == true))
            {
                try { await _binance.Rest.UsdFuturesApi.Trading.CancelOrderAsync(AppConfig.Symbol, orderId: o.Id); } catch { }
            }
        }
    }

    private KlineInterval Interval =>
        AppConfig.Timeframe switch { "1h" => KlineInterval.OneHour, _ => KlineInterval.OneHour };

    private async Task Loop(CancellationToken ct)
    {
        bool hadOpenPosition = false;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_enabled) { await Task.Delay(1000, ct); continue; }

                var klines = await _binance.GetRecentKlines(300, Interval);
                if (klines is null) { await Task.Delay(1000, ct); continue; }

                var closed = klines.Where(k =>
                {
                    var hasFlag = k.GetType().GetProperty("IsClosed") != null;
                    bool isClosed = hasFlag ? (bool)(k.GetType().GetProperty("IsClosed")!.GetValue(k)!) : (k.CloseTime <= DateTime.UtcNow.AddSeconds(-1));
                    return isClosed;
                }).ToList();

                if (closed.Count < AppConfig.EmaSlow + 2) { await Task.Delay(1000, ct); continue; }

                var closes = closed.Select(k => k.ClosePrice).ToList();
                var emaFast = EmaCalculator.Ema(closes, AppConfig.EmaFast);
                var emaSlow = EmaCalculator.Ema(closes, AppConfig.EmaSlow);
                int n = closes.Count;

                decimal prevFast = emaFast[n - 2], prevSlow = emaSlow[n - 2];
                decimal lastFast = emaFast[n - 1], lastSlow = emaSlow[n - 1];
                decimal lastClose = closes[n - 1];

                bool crossUp = prevFast <= prevSlow && lastFast > lastSlow;
                bool crossDown = prevFast >= prevSlow && lastFast < lastSlow;

                DateTime lastClosedKlineTime = closed[n - 1].CloseTime;

                var posInfoRes = await _binance.Rest.UsdFuturesApi.Account.GetPositionInformationAsync(AppConfig.Symbol);
                decimal posQty = 0m;
                if (posInfoRes.Success)
                {
                    var p = posInfoRes.Data.FirstOrDefault();
                    posQty = p?.Quantity ?? 0m;
                    bool isOpen = posQty != 0m;

                    if (hadOpenPosition && !isOpen && _state.EntryPrice.HasValue)
                    {
                        await OnPositionClosed();
                        _state.Reset();
                    }
                    hadOpenPosition = isOpen;
                }

                if (posQty != 0m) await _protection.EnsureProtectiveOrders(lastClose, _state);

                if (_state.CurrentSide == SideDir.None && _state.LastTradeKlineCloseTime != lastClosedKlineTime)
                {
                    if (crossUp)
                    {
                        await _binance.CancelReduceOnlyIfNoPosition();
                        await TryOpen(SideDir.Long, lastClose);
                        _state.LastTradeKlineCloseTime = lastClosedKlineTime;
                    }
                    else if (crossDown)
                    {
                        await _binance.CancelReduceOnlyIfNoPosition();
                        await TryOpen(SideDir.Short, lastClose);
                        _state.LastTradeKlineCloseTime = lastClosedKlineTime;
                    }
                }

                await _wallet.Refresh();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOOP ERR] {ex.Message}");
            }

            try { await Task.Delay(1000, ct); } catch { }
        }
    }

    private async Task TryOpen(SideDir side, decimal refPrice)
    {
        try
        {
            await _binance.SafeSetMarginAndLeverage();

            await _wallet.Refresh();
            var avail = _wallet.LastAvailableBalance ?? 0m;
            var (qty, _, minNotional) = await _binance.ComputeOrderQty(refPrice, avail);
            var positionNotional = qty * refPrice;
            if (positionNotional < minNotional)
                throw new Exception($"MinNotional {minNotional} USDT, current {positionNotional:F2}");

            var entrySide = side == SideDir.Long ? OrderSide.Buy : OrderSide.Sell;
            var res = await _binance.Rest.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol: AppConfig.Symbol,
                side: entrySide,
                type: FuturesOrderType.Market,
                quantity: qty
            );
            if (!res.Success) throw new Exception(res.Error?.ToString() ?? "Entry failed");

            _state.EntryPrice = refPrice;
            _state.EntryQty = qty;
            _state.CurrentSide = side;
            _state.LastOpenTimeUtc = DateTime.UtcNow;

            var postMark = await _binance.GetMarkPrice() ?? refPrice;
            var (tp, _) = await _protection.PlaceProtectiveOrders(side, qty, postMark);
            _state.LastPlannedTpPrice = tp;

            Console.WriteLine($"OPEN {side} @ {refPrice} qty={qty} TP={tp}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TRY OPEN ERR] {ex.Message}");
        }
    }

    private async Task OnPositionClosed()
    {
        string outcome = "Unknown";
        bool byTP = false, bySL = false;

        if (_state.LastOpenTimeUtc.HasValue)
        {
            var from = _state.LastOpenTimeUtc.Value.AddMinutes(-5);
            var allOrdersRes = await _binance.Rest.UsdFuturesApi.Trading.GetOrdersAsync(
                AppConfig.Symbol, startTime: from, endTime: DateTime.UtcNow);

            if (allOrdersRes.Success)
            {
                DateTime Latest(DateTime? a, DateTime? b)
                {
                    var ua = a.GetValueOrDefault(DateTime.MinValue);
                    var ub = b.GetValueOrDefault(DateTime.MinValue);
                    return ua >= ub ? ua : ub;
                }

                var filled = allOrdersRes.Data
                    .Where(o => o.Status == OrderStatus.Filled && o.ReduceOnly == true)
                    .OrderByDescending(o => Latest(o.UpdateTime, o.CreateTime))
                    .ToList();

                var tp = filled.FirstOrDefault(o => o.Type == FuturesOrderType.TakeProfitMarket);
                var sl = filled.FirstOrDefault(o => o.Type == FuturesOrderType.StopMarket);

                if (tp != null && (sl == null || Latest(tp.UpdateTime, tp.CreateTime) >= Latest(sl.UpdateTime, sl.CreateTime))) byTP = true;
                else if (sl != null) bySL = true;
            }
        }

        _stats.TradesTotal++;
        if (byTP) { _stats.Wins++; outcome = "TP"; }
        else if (bySL) { _stats.Losses++; outcome = "SL"; }

        await _wallet.Refresh(true);
        Console.WriteLine($"CLOSED {outcome} | WinRate={_stats.CalcWinRate():F2}% | Wallet={_wallet.LastWalletBalance?.ToString("F2")}");
    }
}
