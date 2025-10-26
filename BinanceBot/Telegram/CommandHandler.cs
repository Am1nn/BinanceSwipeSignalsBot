namespace Bot.Telegram;

using System.Globalization;
using Bot.Config;
using Bot.Trading;
using Bot.Infrastructure;

public sealed class CommandHandler
{
    private readonly TradingEngine _engine;
    private readonly WalletService _wallet;

    public CommandHandler(TradingEngine engine, WalletService wallet)
    {
        _engine = engine; _wallet = wallet;
    }

    public string HelpText() =>
        "*EMA Futures Bot* (LOCKED)\n\n" +
        "🚀 Komutlar\n" +
        "/basla — botu başlat\n" +
        "/dayandir — botu durdur\n" +
        "/stopsession — mevcut pozisyonu kapat\n" +
        "/status — durum\n" +
        "/balans — cüzdan\n" +
        "/stat24 — son 24 saat Realized PnL\n" +
        "/ayarlar — aktif değerler\n\n" +
        "⚙️ Ayarlar\n" +
        "• Leverage: 20x  • TF: 1h\n" +
        "• EMA: 1/24\n" +
        "• TP: 0.5%  • SL: 0.1%\n" +
        "• Pozisyon marjı: *Available USDT'in %92'si*";

    public async Task<string> HandleAsync(string raw)
    {
        var text = raw.Trim().ToLowerInvariant();

        switch (text)
        {
            case "/start": return HelpText();

            case "/basla":
            case "/startbot":
                await _engine.StartAsync();
                return $"🟢 Başladı: {AppConfig.Symbol} 1h | lev=20x | marja=%92";

            case "/dayandir":
            case "/stopbot":
                _engine.Stop();
                return "🛑 Bot durduruldu.";

            case "/stopsession":
                await _engine.StopSessionAsync();
                return "✅ Aktif pozisyon sıfırlandı.";

            case "/balans":
            case "/balance":
                await _wallet.Refresh(true);
                var lw = _wallet.LastWalletBalance?.ToString("F2", CultureInfo.InvariantCulture) ?? "?";
                var la = _wallet.LastAvailableBalance?.ToString("F2", CultureInfo.InvariantCulture) ?? "?";
                var sw = _wallet.StartWalletBalance ?? 0m;
                var delta = (_wallet.LastWalletBalance ?? 0m) - sw;
                var pct = sw != 0 ? (delta / sw) * 100m : 0m;
                return
                    $"💼 Cüzdan: *{lw}* USDT\n" +
                    $"🧮 Available: *{la}* USDT\n" +
                    $"⚙️ EMA: {AppConfig.EmaFast}/{AppConfig.EmaSlow} | TP: {(AppConfig.TpPct * 100m):F3}% | SL: {(AppConfig.SlPct * 100m):F3}%\n" +
                    $"🧮 Marja: %92 | Lev: 20x | TF: 1h | Symbol: {AppConfig.Symbol}\n" +
                    $"📈 PnL (sess): *{delta.ToString("F2", CultureInfo.InvariantCulture)}* USDT (*{pct.ToString("F3", CultureInfo.InvariantCulture)}%*)\n" +
                    $"🏆 Win rate: *{_engine.Stats.CalcWinRate():F2}%*";

            case "/stat24":
            case "/stats24h":
                // İstersen buraya eski Program.cs'deki income sorgusunu aynen taşıyabilirsin.
                return "📊 stat24: Bu metod kısa tutuldu; istersek gelir geçmişi çağırıp toplayalım.";

            case "/status":
                await _wallet.Refresh();
                var bal = _wallet.LastWalletBalance?.ToString("F2", CultureInfo.InvariantCulture) ?? "?";
                var avail = _wallet.LastAvailableBalance?.ToString("F2", CultureInfo.InvariantCulture) ?? "?";
                var sessSide = _engine.State.CurrentSide.ToString();
                var openedAt = _engine.State.LastOpenTimeUtc.HasValue
                    ? AppConfig.AztNowString(_engine.State.LastOpenTimeUtc.Value)
                    : "-";
                var lastTp = _engine.State.LastPlannedTpPrice?.ToString($"F{_engine.State.LastPlannedTpPrice?.ToString().Split('.').LastOrDefault()?.Length ?? 2}", CultureInfo.InvariantCulture) ?? "-";
                return
                    $"Vəziyyət: {(_engine.IsEnabled ? "🟢 AKTİV" : "🛑 DAYANIB")}\n" +
                    $"Simvol: {AppConfig.Symbol} | Leverage: 20x | TF: 1h\n" +
                    $"Cüzdan: {bal} | Mövcud: {avail}\n" +
                    $"Sessiya: {sessSide} | Açılış: {openedAt}\n" +
                    $"Son TP (plan): {lastTp}\n" +
                    $"🏆 Win rate: *{_engine.Stats.CalcWinRate():F2}%*";

            case "/ayarlar":
            case "/settings":
                return
                    "🔧 *Aktiv Ayarlar (Kilidli)*\n" +
                    $"- Symbol: `{AppConfig.Symbol}`  TF: `1h`\n" +
                    $"- EMA: `{AppConfig.EmaFast}/{AppConfig.EmaSlow}`\n" +
                    $"- TP: `0.5%`  SL: `0.1%`\n" +
                    $"- Leverage: `20x`\n" +
                    $"- Pozisyon marjı: `Available USDT'in %92'si`\n\n" +
                    "Re-entry: *DEAKTİF* (yalnız kəsişmədə bir dəfə açılır)";

            default:
                if (raw.StartsWith("/set")) return "⚠️ Ayarlar kilitli: 1h, EMA 1/24, TP 0.5%, SL 0.1%, %92 marj.";
                return "❓ Bilinmeyen komut. /start";
        }
    }
}
