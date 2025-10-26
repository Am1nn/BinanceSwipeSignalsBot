namespace Bot.Telegram;

using System.Net.Http.Json;
using Bot.Config;

public sealed class TelegramService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(100) };
    private readonly CommandHandler _handler;
    private int? _offset;

    public TelegramService(CommandHandler handler) => _handler = handler;

    private string TgBase => $"https://api.telegram.org/bot{AppConfig.TelegramBotToken}/";

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            var me = await Get<TgResponse<TgUser>>("getMe");
            Console.WriteLine(me?.ok == true ? $"Telegram Bot ID: {me!.result!.id}" : "getMe failed.");
        }
        catch { }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = $"{TgBase}getUpdates?timeout=30&allowed_updates=%5B%22message%22%2C%22callback_query%22%5D" + (_offset.HasValue ? $"&offset={_offset}" : "");
                var resp = await _http.GetFromJsonAsync<TgResponse<TgUpdate[]>>(url, ct);
                if (resp?.ok != true || resp!.result == null) { await Task.Delay(500, ct); continue; }

                foreach (var u in resp.result)
                {
                    _offset = u.update_id + 1;

                    if (u.callback_query != null)
                    {
                        var userId = u.callback_query.from?.id ?? 0;
                        if (!AppConfig.AllowedUserIds.Contains(userId))
                        {
                            await AnswerCallback(u.callback_query.id, "⛔ Yetkisiz");
                            continue;
                        }
                        await AnswerCallback(u.callback_query.id, "✅");
                        continue;
                    }

                    var msg = u.message;
                    if (msg == null || string.IsNullOrWhiteSpace(msg.text)) continue;

                    long chatId = msg.chat.id;
                    long userId2 = msg.from?.id ?? 0;
                    if (!AppConfig.AllowedUserIds.Contains(userId2))
                    {
                        await SendMessage(chatId, "⛔ Bu botu istifadə səlahiyyətiniz yoxdur.");
                        continue;
                    }

                    var answer = await _handler.HandleAsync(msg.text);
                    await SendMessage(chatId, answer, "Markdown");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TG LOOP] {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task<T?> Get<T>(string method)
    {
        try { return await _http.GetFromJsonAsync<T>($"{TgBase}{method}"); }
        catch { return default; }
    }

    private async Task SendMessage(long chatId, string text, string? parseMode = null)
    {
        var payload = new Dictionary<string, object?> { ["chat_id"] = chatId, ["text"] = text };
        if (!string.IsNullOrEmpty(parseMode)) payload["parse_mode"] = parseMode;
        var resp = await _http.PostAsJsonAsync($"{TgBase}sendMessage", payload);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[TG SEND ERR] {resp.StatusCode} {body}");
        }
    }

    private async Task AnswerCallback(string callbackId, string text = "", bool showAlert = false)
    {
        var payload = new Dictionary<string, object?> { ["callback_query_id"] = callbackId, ["text"] = text, ["show_alert"] = showAlert };
        await _http.PostAsJsonAsync($"{TgBase}answerCallbackQuery", payload);
    }
}
