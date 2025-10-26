# 🟢 BinanceSwipeSignalsBot

BinanceSwipeSignalsBot — **EMA(1)** və **EMA(24)** göstəricilərinin kəsişməsinə əsaslanan,
**Binance USDT-M Futures** bazarında **20x leverage** ilə avtomatik ticarət edən **Telegram idarəetməli bot**dur.

> ⚠️ **Qeyd**: Bu bot yalnız **tədris və eksperimental məqsədlər** üçün nəzərdə tutulmuşdur.
---

## 🚀 Əsas Xüsusiyyətlər

* 📈 **EMA Strategiyası** – EMA1, EMA24-ü **altdan yuxarı** kəsdikdə **Long**, əks halda **Short** açır.
* 🎯 **Avtomatik TP/SL** – Hər açılmış mövqeyə `reduceOnly` **TakeProfitMarket** və **StopMarket** sifarişləri avtomatik əlavə olunur.
* ⚙️ **Sabit Parametrlər** –

  * Leverage: **20x**
  * Timeframe: **1 saat (1h)**
  * EMA: **1 / 24**
  * Take Profit: **+0.5%**
  * Stop Loss: **−0.1%**
  * Marja istifadəsi: **Available USDT-in 92%-i**
* 💬 **Telegram nəzarəti** – Botu idarə etmək üçün sadə əmrlər:

  * `/start` – kömək mətni
  * `/basla` – botu işə sal
  * `/dayandir` – botu dayandır
  * `/stopsession` – mövcud mövqeni bağla
  * `/status` – vəziyyəti göstər
  * `/balans` – cüzdan məlumatı
  * `/stat24` – son 24 saatın Realized PnL statistikası
  * `/ayarlar` – cari aktiv parametrləri göstərir
* 🔐 **Isolated + One-way rejimi** – Mövqelər təhlükəsiz şəkildə idarə olunur.
* 🧠 **Statistika yaddaşı** – Qələbə/Məğlubiyyət nisbəti, sessiya nəticələri və qazanclar saxlanılır.

---

## 🧩 Layihə Quruluşu

Layihə **təmiz və modulyar memarlıq** prinsipi ilə qurulub:

```
/Bot
  Program.cs                → giriş nöqtəsi
  /Config                   → AppConfig (API, Token, sabitlər)
  /Domain                   → Enums və modellər (SideDir, Stats, Session)
  /Infrastructure
    BinanceService          → Binance REST əməliyyatları
    SymbolFilterCache       → Tick və Step ölçüləri
    WalletService           → Cüzdan balansı və hesablama
  /Trading
    TradingEngine           → Əsas ticarət döngüsü (EMA kəsişmə məntiqi)
    EmaCalculator           → EMA hesablamaları
    OrderProtectionService  → TP/SL qoruma mexanizmi
  /Telegram
    TelegramService         → Telegram API əlaqəsi
    CommandHandler          → Əmrlərin emalı
  /Utils
    MathUtils, FormatUtils  → köməkçi funksiyalar
```

---

## 🔧 Qurulum və İşə Salma

### 1. Lazımi mühit

* .NET 8 SDK
* NuGet paketləri:

  ```
  Binance.Net
  CryptoExchange.Net
  ```

### 2. Ətraf Mühit Dəyişənləri

Serverdə və ya `.env` faylında aşağıdakı dəyişənləri əlavə et:

```bash
BINANCE_API_KEY=senin_api_key
BINANCE_API_SECRET=senin_api_secret
TG_BOT_TOKEN=senin_telegram_token
```

> Telegram token-i **@BotFather** vasitəsilə yaradıla bilər.

### 3. Layihəni işə sal

```bash
dotnet restore
dotnet run --project BinanceSwipeSignalsBot
```

Bot başladıqda konsolda belə bir mesaj görünəcək:

```
🤖 Bot başladı. Komandalar üçün /start yazın.
```

---

## 📊 İş Prinsipi

1. Hər 1 saatlıq şam qapanışında **EMA(1)** və **EMA(24)** dəyərləri hesablanır.
2. Kəsişmə baş verərsə:

   * EMA1 > EMA24 → Long
   * EMA1 < EMA24 → Short
3. Mövqe açıldıqdan dərhal sonra:

   * **TP = +0.5%**
   * **SL = −0.1%**
     sifarişləri yerləşdirilir.
4. Mövqe bağlandıqda nəticə (TP və ya SL) Telegram-da bildirilir.

---

## 🧠 Nümunə Telegram Mesajları

```
🟢 Pozisiya açıldı — BTCUSDT  
Yön: Long | Giriş: 107,000.5$ | Qty: 0.014  
TP (plan): 107,535.0$ | SL: 106,890.0$ | Lev: 20x
```

```
🔴 Pozisiya bağlandı — BTCUSDT  
Nəticə: TP | Win rate: 66.7%  
💼 Balans: 155.24 USDT
```

---

## 🧰 Faydalı Əmr Cədvəli

| Əmr            | İzah                             |
| -------------- | -------------------------------- |
| `/start`       | Kömək və qaydalar                |
| `/basla`       | Botu işə salır                   |
| `/dayandir`    | Botu dayandırır                  |
| `/stopsession` | Mövcud mövqeni bağlayır          |
| `/status`      | Cari vəziyyəti göstərir          |
| `/balans`      | Cüzdan məlumatı                  |
| `/stat24`      | 24 saatlıq Realized PnL          |
| `/ayarlar`     | Cari aktiv parametrləri göstərir |

---

---

## 👨‍💻 Müəllif və Lisenziya

**Layihə:** BinanceSwipeSignalsBot
**Müəllif:** [Amin Bənnayev](https://github.com/Am1nn)

