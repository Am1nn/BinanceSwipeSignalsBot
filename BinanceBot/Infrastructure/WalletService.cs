namespace Bot.Infrastructure;

using System.Globalization;

public sealed class WalletService
{
    private readonly BinanceService _binance;

    public decimal? StartWalletBalance { get; private set; }
    public decimal? LastWalletBalance { get; private set; }
    public decimal? LastAvailableBalance { get; private set; }

    public WalletService(BinanceService binance) => _binance = binance;

    public async Task Refresh(bool verbose = false)
    {
        try
        {
            var balRes = await _binance.Rest.UsdFuturesApi.Account.GetBalancesAsync();
            if (!balRes.Success) { if (verbose) Console.WriteLine($"[Wallet] Error: {balRes.Error}"); return; }

            var usdt = balRes.Data.FirstOrDefault(b => string.Equals(b.Asset, "USDT", StringComparison.OrdinalIgnoreCase));
            if (usdt == null) { if (verbose) Console.WriteLine("[Wallet] USDT not found."); return; }

            LastWalletBalance = FirstNonNullDecimal(GetDec(usdt, "WalletBalance"), GetDec(usdt, "Balance"), GetDec(usdt, "CrossWalletBalance"));
            LastAvailableBalance = FirstNonNullDecimal(GetDec(usdt, "AvailableBalance"), GetDec(usdt, "MaxWithdrawAmount"));

            if (StartWalletBalance is null) StartWalletBalance = LastWalletBalance;
        }
        catch (Exception ex) { if (verbose) Console.WriteLine($"[Wallet] Exception: {ex.Message}"); }
    }

    private static decimal? GetDec(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop);
        var v = p?.GetValue(obj);
        if (v == null) return null;
        if (v is decimal d) return d;
        if (v is double dbl) return (decimal)dbl;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? FirstNonNullDecimal(params decimal?[] values)
    {
        foreach (var v in values) if (v.HasValue) return v.Value;
        return null;
    }
}
