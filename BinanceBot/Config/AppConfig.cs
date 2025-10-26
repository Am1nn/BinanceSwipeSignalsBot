namespace Bot.Config;

using System;
using System.Globalization;

public static class AppConfig
{
    // 🔒 Kilitli parametreler ve varsayılanlar (ENV varsa ENV kazanır)
    public static string ApiKey => EnvOr("BINANCE_API_KEY", DEF_API_KEY);
    public static string ApiSecret => EnvOr("BINANCE_API_SECRET", DEF_API_SECRET);
    public static string TelegramBotToken => EnvOr("TG_BOT_TOKEN", DEF_TELEGRAM_BOT_TOKEN);

    // UYARI: Gerçek anahtarları repo'da bırakma.
    private const string DEF_API_KEY = "REPLACE_ME";
    private const string DEF_API_SECRET = "REPLACE_ME";
    private const string DEF_TELEGRAM_BOT_TOKEN = "REPLACE_ME";

    public static readonly long[] AllowedUserIds = { 7130953766, 1262160420 };

    // Strateji (kilitli)
    public const string Symbol = "BTCUSDT";
    public const int Leverage = 20;
    public const int EmaFast = 1;
    public const int EmaSlow = 24;
    public const decimal TpPct = 0.005m; // +0.5%
    public const decimal SlPct = 0.001m; // -0.1%
    public const string Timeframe = "1h";
    public const decimal MarginUseRatio = 0.92m;

    public static string AztNowString(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku"))
        .ToString("dd.MM.yyyy HH:mm:ss 'AZT'", CultureInfo.InvariantCulture);

    private static string EnvOr(string key, string defVal)
        => Environment.GetEnvironmentVariable(key) is string v && !string.IsNullOrWhiteSpace(v) ? v : defVal;
}
