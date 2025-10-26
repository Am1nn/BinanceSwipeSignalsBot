namespace Bot.Telegram;

public class TgResponse<T> { public bool ok { get; set; } public T? result { get; set; } public string? description { get; set; } }
public class TgUpdate { public int update_id { get; set; } public TgMessage? message { get; set; } public TgCallbackQuery? callback_query { get; set; } }
public class TgMessage { public long message_id { get; set; } public TgUser? from { get; set; } public TgChat chat { get; set; } = new(); public string? text { get; set; } public long date { get; set; } }
public class TgUser { public long id { get; set; } public bool is_bot { get; set; } public string? first_name { get; set; } public string? username { get; set; } }
public class TgChat { public long id { get; set; } public string? type { get; set; } public string? title { get; set; } public string? username { get; set; } public string? first_name { get; set; } public string? last_name { get; set; } }
public class TgCallbackQuery { public string id { get; set; } = ""; public TgUser? from { get; set; } public TgMessage? message { get; set; } public string? data { get; set; } }
