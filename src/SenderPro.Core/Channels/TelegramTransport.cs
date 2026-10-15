using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SenderPro.Core;

/// <summary>
/// Official Telegram Bot API adapter. Bots can message chats that have authorized/interacted with them;
/// a phone number is not a Telegram chat ID. The caller owns HttpClient and must not attach URI logging
/// handlers, since the Bot API embeds the credential in its URL. No automatic retries are performed.
/// </summary>
public sealed class TelegramTransport : ISocialTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _token;

    public TelegramTransport(HttpClient httpClient, string token)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (string.IsNullOrWhiteSpace(token) || !Regex.IsMatch(token, @"\A[0-9]+:[A-Za-z0-9_-]+\z"))
            throw ChannelHttp.InvalidConfiguration("أدخل رمز بوت Telegram صحيحًا من BotFather.");
        _httpClient = httpClient;
        _token = token;
    }

    public async Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(message);
        var recipient = message.Recipient?.Trim() ?? "";
        var numeric = Regex.IsMatch(recipient, @"\A-?[1-9][0-9]*\z") &&
            long.TryParse(recipient, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out var chatId) && chatId != 0;
        if (!numeric && !Regex.IsMatch(recipient, @"\A@[A-Za-z][A-Za-z0-9_]{4,31}\z"))
            throw ChannelHttp.InvalidMessage("مستلم Telegram يجب أن يكون Chat ID أو اسم قناة بصيغة @username.");
        if (string.IsNullOrWhiteSpace(message.Body) || message.Body.EnumerateRunes().Count() > 4096)
            throw ChannelHttp.InvalidMessage("رسالة Telegram يجب أن تحتوي من حرف إلى 4096 حرفًا.");

        using var request = Request(HttpMethod.Post, "sendMessage");
        request.Content = JsonContent.Create(new { chat_id = recipient, text = message.Body });
        using var response = await ChannelHttp.SendAsync(_httpClient, request, true, cancellationToken)
            .ConfigureAwait(false);
        if (!response.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("message_id", out var id) || id.ValueKind != JsonValueKind.Number ||
            !id.TryGetInt64(out var messageId) || messageId <= 0)
            throw ChannelHttp.InvalidResponse();
        return new ChannelReceipt(ChannelKind.Telegram, messageId.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow);
    }

    public async Task<string> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = Request(HttpMethod.Get, "getMe");
        using var response = await ChannelHttp.SendAsync(_httpClient, request, true, cancellationToken)
            .ConfigureAwait(false);
        if (!response.RootElement.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("id", out var id) ||
            id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var botId) || botId <= 0 ||
            !result.TryGetProperty("is_bot", out var isBot) || isBot.ValueKind != JsonValueKind.True)
            throw ChannelHttp.InvalidResponse();
        var username = ChannelHttp.String(result, "username");
        return !string.IsNullOrWhiteSpace(username) &&
            Regex.IsMatch(username, @"\A[A-Za-z][A-Za-z0-9_]{4,31}\z")
            ? "@" + username : "Bot " + botId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads at most the first 100 pending bot updates. No offset is supplied, so this request does not
    /// acknowledge them; allowed_updates is omitted to preserve the bot's existing subscription.
    /// Does not delete webhooks, change bot settings, or retain any message body.
    /// Telegram keeps pending updates for a limited time, so this is not a complete contact directory.
    /// </summary>
    public async Task<IReadOnlyList<TelegramChat>> GetRecentChatsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = Request(HttpMethod.Get, "getUpdates?limit=100&timeout=0");
        using var response = await ChannelHttp.SendAsync(_httpClient, request, true, cancellationToken)
            .ConfigureAwait(false);
        if (!response.RootElement.TryGetProperty("result", out var updates) ||
            updates.ValueKind != JsonValueKind.Array)
            throw InvalidChatsResponse();

        var chats = new Dictionary<string, TelegramChat>(StringComparer.Ordinal);
        foreach (var update in updates.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (update.ValueKind != JsonValueKind.Object) throw InvalidChatsResponse();
            foreach (var updateType in new[] { "message", "channel_post", "my_chat_member" })
            {
                if (!update.TryGetProperty(updateType, out var entry)) continue;
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("chat", out var chat) ||
                    chat.ValueKind != JsonValueKind.Object || !chat.TryGetProperty("id", out var idValue) ||
                    idValue.ValueKind != JsonValueKind.Number || !idValue.TryGetInt64(out var numericId) ||
                    numericId == 0)
                    throw InvalidChatsResponse();
                var type = ChannelHttp.String(chat, "type");
                if (type is not ("private" or "group" or "supergroup" or "channel"))
                    throw InvalidChatsResponse();
                var id = numericId.ToString(CultureInfo.InvariantCulture);
                var name = ChatName(chat, id);
                // Later updates may contain a newer title/name for the same chat.
                chats[id] = new TelegramChat(id, name, type);
            }
        }
        return chats.Values.ToArray();
    }

    private string ChatName(JsonElement chat, string fallback)
    {
        var name = ChannelHttp.String(chat, "title")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = string.Join(" ", new[] { ChannelHttp.String(chat, "first_name"),
                ChannelHttp.String(chat, "last_name") }.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        if (string.IsNullOrWhiteSpace(name) && ChannelHttp.String(chat, "username") is { } username &&
            Regex.IsMatch(username, @"\A[A-Za-z][A-Za-z0-9_]{4,31}\z"))
            name = "@" + username;
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        // Never echo the credential, even if an unexpected chat title happens to contain it.
        name = name.Replace(_token, "[redacted]", StringComparison.Ordinal);
        name = new string(name.Select(character => char.IsControl(character) ? ' ' : character).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        return name.Length > 256 ? name[..256] : name;
    }

    private static ChannelTransportException InvalidChatsResponse() => new(ChannelErrorKind.InvalidResponse,
        "أعادت Telegram قائمة محادثات غير صالحة؛ لم يتم استيراد المحادثات.");

    private HttpRequestMessage Request(HttpMethod method, string operation) =>
        new(method, new Uri($"https://api.telegram.org/bot{_token}/{operation}"));
}
