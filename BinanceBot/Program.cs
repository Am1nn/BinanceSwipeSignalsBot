using Bot.Config;
using Bot.Infrastructure;
using Bot.Trading;
using Bot.Telegram;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (string.IsNullOrWhiteSpace(AppConfig.TelegramBotToken)) { Console.WriteLine("❌ TELEGRAM_BOT_TOKEN boşdur."); return; }
if (string.IsNullOrWhiteSpace(AppConfig.ApiKey) || string.IsNullOrWhiteSpace(AppConfig.ApiSecret)) { Console.WriteLine("❌ Binance API açarları boşdur."); return; }

var binance = new BinanceService();
await binance.InitAsync();

var wallet = new WalletService(binance);
await wallet.Refresh(true);
if (wallet.StartWalletBalance is null) { Console.WriteLine("❌ Futures USDT balansı oxunmadı."); return; }

var protection = new OrderProtectionService(binance);
var engine = new TradingEngine(binance, wallet, protection);
var handler = new CommandHandler(engine, wallet);
var tg = new TelegramService(handler);

Console.WriteLine("🤖 Bot başladı. Komutlar için /start yazın.");
await tg.RunAsync(); // sonsuz döngü
