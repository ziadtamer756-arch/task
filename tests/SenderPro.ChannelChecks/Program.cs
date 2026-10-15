using System.Net;
using System.Text;
using System.Text.Json;
using SenderPro.Core;

const string TelegramToken = "123456789:fake_bot_test_token";
const string WhatsAppToken = "fake_whatsapp_secret_never_log";
var tests = new List<(string Name, Func<Task> Run)>
{
    ("Telegram serializes signed chat ID and text, fixed HTTPS endpoint, returns provider ID", async () =>
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            Equal(HttpMethod.Post, request.Method);
            Equal($"https://api.telegram.org/bot{TelegramToken}/sendMessage", request.RequestUri!.AbsoluteUri);
            Equal(null, request.Headers.Authorization);
            Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var json = await ReadRequest(request, token);
            Equal("-1001234567890", json.RootElement.GetProperty("chat_id").GetString());
            Equal("مرحبًا\nملف عربي", json.RootElement.GetProperty("text").GetString());
            False(json.RootElement.TryGetProperty("parse_mode", out _));
            return Json("{\"ok\":true,\"result\":{\"message_id\":42}}");
        });
        using var client = new HttpClient(handler);
        var receipt = await new TelegramTransport(client, TelegramToken).SendAsync(
            new ChannelMessage { Recipient = "-1001234567890", Body = "مرحبًا\nملف عربي" }, default);
        Equal(ChannelKind.Telegram, receipt.Channel);
        Equal("42", receipt.MessageId);
        True(receipt.AcceptedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }),
    ("Telegram getMe validates bot identity without sending messages", async () =>
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Equal(HttpMethod.Get, request.Method);
            True(request.RequestUri!.AbsolutePath.EndsWith("/getMe", StringComparison.Ordinal));
            return Task.FromResult(Json("{\"ok\":true,\"result\":{\"id\":123456789,\"is_bot\":true,\"username\":\"example_bot\"}}"));
        });
        using var client = new HttpClient(handler);
        Equal("@example_bot", await new TelegramTransport(client, TelegramToken).CheckConnectionAsync(default));
        Equal(1, handler.CallCount);
    }),
    ("Telegram recent chats reads pending updates without acknowledging or changing filters", async () =>
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Equal(HttpMethod.Get, request.Method);
            Equal($"https://api.telegram.org/bot{TelegramToken}/getUpdates?limit=100&timeout=0", request.RequestUri!.AbsoluteUri);
            False(request.RequestUri.Query.Contains("offset", StringComparison.Ordinal));
            False(request.RequestUri.Query.Contains("allowed_updates", StringComparison.Ordinal));
            Equal(null, request.Content);
            return Task.FromResult(Json("""
                {"ok":true,"result":[
                  {"update_id":1,"message":{"chat":{"id":12345678,"type":"private","first_name":"أحمد","last_name":"علي"},"text":"message body is not a contact name"}},
                  {"update_id":2,"channel_post":{"chat":{"id":-1001234567890,"type":"channel","title":"إعلانات الشركة"},"text":"private channel post"}},
                  {"update_id":3,"my_chat_member":{"chat":{"id":-987654321,"type":"group","title":"فريق العمل"},"new_chat_member":{"status":"member"}}},
                  {"update_id":4,"message":{"chat":{"id":12345678,"type":"private","first_name":"أحمد","last_name":"حسن"},"text":"another body"}},
                  {"update_id":5,"callback_query":{"data":"never returned"}}
                ]}
                """));
        });
        using var client = new HttpClient(handler);
        var chats = await new TelegramTransport(client, TelegramToken).GetRecentChatsAsync(default);
        Equal(3, chats.Count);
        Equal(new TelegramChat("12345678", "أحمد حسن", "private"), chats.Single(chat => chat.Id == "12345678"));
        Equal(new TelegramChat("-1001234567890", "إعلانات الشركة", "channel"), chats.Single(chat => chat.Type == "channel"));
        Equal(new TelegramChat("-987654321", "فريق العمل", "group"), chats.Single(chat => chat.Type == "group"));
        False(string.Join(" ", chats.Select(chat => chat.Name)).Contains("body", StringComparison.Ordinal));
        Equal(1, handler.CallCount);
    }),
    ("Telegram chat names use username/id fallbacks and redact credential", async () =>
    {
        var body = """
            {"ok":true,"result":[
              {"message":{"chat":{"id":123,"type":"private","username":"example_user"}}},
              {"message":{"chat":{"id":456,"type":"private"}}},
              {"message":{"chat":{"id":-789,"type":"supergroup","title":"Secret TOKEN_PLACEHOLDER"}}}
            ]}
            """.Replace("TOKEN_PLACEHOLDER", TelegramToken, StringComparison.Ordinal);
        using var handler = Respond(body);
        using var client = new HttpClient(handler);
        var chats = await new TelegramTransport(client, TelegramToken).GetRecentChatsAsync(default);
        Equal("@example_user", chats.Single(chat => chat.Id == "123").Name);
        Equal("456", chats.Single(chat => chat.Id == "456").Name);
        Equal("Secret [redacted]", chats.Single(chat => chat.Id == "-789").Name);
    }),
    ("Telegram empty pending updates return no chats", async () =>
    {
        using var handler = Respond("{\"ok\":true,\"result\":[]}");
        using var client = new HttpClient(handler);
        Equal(0, (await new TelegramTransport(client, TelegramToken).GetRecentChatsAsync(default)).Count);
    }),
    ("Telegram malformed chat/update shapes fail without partially importing contacts", async () =>
    {
        foreach (var body in new[] { "{\"ok\":true}", "{\"ok\":true,\"result\":{}}",
            "{\"ok\":true,\"result\":[null]}", "{\"ok\":true,\"result\":[{\"message\":{}}]}",
            "{\"ok\":true,\"result\":[{\"message\":{\"chat\":{\"id\":\"123\",\"type\":\"private\"}}}]}",
            "{\"ok\":true,\"result\":[{\"message\":{\"chat\":{\"id\":0,\"type\":\"private\"}}}]}",
            "{\"ok\":true,\"result\":[{\"my_chat_member\":{\"chat\":{\"id\":123}}}]}" })
        {
            using var handler = Respond(body);
            using var client = new HttpClient(handler);
            await Error(ChannelErrorKind.InvalidResponse, () => new TelegramTransport(client, TelegramToken)
                .GetRecentChatsAsync(default));
        }
    }),
    ("Telegram polling409 explains existing integration and does not change webhook/settings", async () =>
    {
        using var handler = new FakeHandler((request, _) =>
        {
            True(request.RequestUri!.AbsolutePath.EndsWith("/getUpdates", StringComparison.Ordinal));
            return Task.FromResult(Json($"{{\"ok\":false,\"error_code\":409,\"description\":\"{TelegramToken}\"}}", HttpStatusCode.Conflict));
        });
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.Rejected, () => new TelegramTransport(client, TelegramToken)
            .GetRecentChatsAsync(default));
        Equal(409, ex.ProviderCode);
        True(ex.Message.Contains("Webhook", StringComparison.Ordinal));
        False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
        Equal(1, handler.CallCount);
    }),
    ("Telegram rejects phone numbers represented with + before making a request", async () =>
    {
        using var handler = NeverCalled();
        using var client = new HttpClient(handler);
        await Error(ChannelErrorKind.InvalidMessage, () => new TelegramTransport(client, TelegramToken)
            .SendAsync(new ChannelMessage { Recipient = "+201012345678", Body = "hello" }, default));
        Equal(0, handler.CallCount);
    }),
    ("Telegram HTTP200 ok:false rejects and retains only numeric retry metadata", async () =>
    {
        var ex = await TelegramError(Json($"{{\"ok\":false,\"error_code\":429,\"description\":\"{TelegramToken}\",\"parameters\":{{\"retry_after\":9}}}}"), ChannelErrorKind.RateLimited);
        Equal(429, ex.ProviderCode);
        Equal(9, ex.RetryAfterSeconds);
        False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
    }),
    ("Telegram non-JSON HTTP401 becomes authentication error without body leak", async () =>
    {
        var ex = await TelegramError(Json(TelegramToken, HttpStatusCode.Unauthorized), ChannelErrorKind.Authentication);
        Equal(HttpStatusCode.Unauthorized, ex.HttpStatusCode);
        False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
    }),
    ("Telegram blocked-chat HTTP403 is recipient rejection, not account authentication failure", async () =>
    {
        var ex = await TelegramError(Json("{\"ok\":false,\"error_code\":403,\"description\":\"Forbidden: bot was blocked by the user\"}",
            HttpStatusCode.Forbidden), ChannelErrorKind.Rejected);
        Equal(403, ex.ProviderCode);
    }),
    ("Telegram malformed success cannot masquerade as accepted", async () =>
    {
        foreach (var body in new[] { "not JSON", "{}", "{\"ok\":true}", "{\"ok\":true,\"result\":[]}",
            "{\"ok\":true,\"result\":{\"message_id\":0}}", "{\"ok\":true,\"result\":{\"message_id\":\"42\"}}" })
        {
            using var handler = Respond(body);
            using var client = new HttpClient(handler);
            await AnyChannelError(() => new TelegramTransport(client, TelegramToken).SendAsync(TelegramMessage(), default));
        }
    }),
    ("Telegram HTTP exception URI and token never escape", async () =>
    {
        using var handler = new FakeHandler((request, _) => throw new HttpRequestException(request.RequestUri!.ToString()));
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.Network, () => new TelegramTransport(client, TelegramToken)
            .SendAsync(TelegramMessage(), default));
        Equal(null, ex.InnerException);
        False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
    }),
    ("Transport timeout is a sanitized network error", async () =>
    {
        using var handler = new FakeHandler((request, _) => throw new TaskCanceledException(request.RequestUri!.ToString()));
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.Network, () => new TelegramTransport(client, TelegramToken)
            .SendAsync(TelegramMessage(), default));
        False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
    }),
    ("Cancellation during request propagates cancellation without raw URI", async () =>
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new FakeHandler((request, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(request.RequestUri!.ToString(), cancellation.Token);
        });
        using var client = new HttpClient(handler);
        try
        {
            await new TelegramTransport(client, TelegramToken).SendAsync(TelegramMessage(), cancellation.Token);
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException ex)
        {
            True(ex.CancellationToken.IsCancellationRequested);
            False(ex.ToString().Contains(TelegramToken, StringComparison.Ordinal));
        }
    }),
    ("Already cancelled check makes no request", async () =>
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var handler = NeverCalled();
        using var client = new HttpClient(handler);
        try
        {
            await new TelegramTransport(client, TelegramToken).CheckConnectionAsync(cancellation.Token);
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException) { }
        Equal(0, handler.CallCount);
    }),
    ("HTTP timeout also bounds a stalled response body", async () =>
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledReadStream())
        }));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };
        await Error(ChannelErrorKind.Network, () => new TelegramTransport(client, TelegramToken)
            .SendAsync(TelegramMessage(), default));
        Equal(1, handler.CallCount);
    }),
    ("WhatsApp template uses Bearer authorization and official Graph payload", async () =>
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            Equal(HttpMethod.Post, request.Method);
            Equal("https://graph.facebook.com/v23.0/123456789/messages", request.RequestUri!.AbsoluteUri);
            Equal("Bearer", request.Headers.Authorization!.Scheme);
            Equal(WhatsAppToken, request.Headers.Authorization.Parameter);
            False(request.RequestUri.AbsoluteUri.Contains(WhatsAppToken, StringComparison.Ordinal));
            using var json = await ReadRequest(request, token);
            var root = json.RootElement;
            Equal("whatsapp", root.GetProperty("messaging_product").GetString());
            Equal("individual", root.GetProperty("recipient_type").GetString());
            Equal("201012345678", root.GetProperty("to").GetString());
            Equal("template", root.GetProperty("type").GetString());
            var template = root.GetProperty("template");
            Equal("order_update", template.GetProperty("name").GetString());
            Equal("ar", template.GetProperty("language").GetProperty("code").GetString());
            var component = template.GetProperty("components")[0];
            Equal("body", component.GetProperty("type").GetString());
            Equal("text", component.GetProperty("parameters")[0].GetProperty("type").GetString());
            Equal("أحمد", component.GetProperty("parameters")[0].GetProperty("text").GetString());
            Equal("42", component.GetProperty("parameters")[1].GetProperty("text").GetString());
            False(root.TryGetProperty("text", out _));
            return WhatsAppSuccess();
        });
        using var client = new HttpClient(handler);
        var receipt = await WhatsApp(client).SendAsync(WhatsAppMessage(), default);
        Equal("wamid.test123", receipt.MessageId);
        Equal(ChannelKind.WhatsApp, receipt.Channel);
        Equal("accepted", receipt.ProviderState);
        Equal(null, client.DefaultRequestHeaders.Authorization);
    }),
    ("WhatsApp quality-held messages preserve held provider state", async () =>
    {
        using var handler = Respond("{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.qualityhold\",\"message_status\":\"held_for_quality_assessment\"}]}");
        using var client = new HttpClient(handler);
        var receipt = await WhatsApp(client).SendAsync(WhatsAppMessage(), default);
        Equal("held_for_quality_assessment", receipt.ProviderState);
        Equal("wamid.qualityhold", receipt.MessageId);
    }),
    ("WhatsApp static template omits components", async () =>
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            using var json = await ReadRequest(request, token);
            False(json.RootElement.GetProperty("template").TryGetProperty("components", out _));
            return WhatsAppSuccess();
        });
        using var client = new HttpClient(handler);
        var message = WhatsAppMessage();
        message.WhatsAppTemplate!.BodyParameters.Clear();
        await WhatsApp(client).SendAsync(message, default);
    }),
    ("WhatsApp free text requires explicit service-window mode", async () =>
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            using var json = await ReadRequest(request, token);
            Equal("text", json.RootElement.GetProperty("type").GetString());
            Equal("رد على العميل", json.RootElement.GetProperty("text").GetProperty("body").GetString());
            False(json.RootElement.GetProperty("text").GetProperty("preview_url").GetBoolean());
            False(json.RootElement.TryGetProperty("template", out _));
            return WhatsAppSuccess();
        });
        using var client = new HttpClient(handler);
        await WhatsApp(client).SendAsync(new ChannelMessage
        {
            Recipient = "201012345678", Body = "رد على العميل",
            WhatsAppMode = WhatsAppMessageMode.ServiceWindowText
        }, default);
    }),
    ("WhatsApp default prevents accidental free-text business initiation", async () =>
    {
        using var handler = NeverCalled();
        using var client = new HttpClient(handler);
        await Error(ChannelErrorKind.InvalidMessage, () => WhatsApp(client).SendAsync(
            new ChannelMessage { Recipient = "201012345678", Body = "hello" }, default));
        Equal(0, handler.CallCount);
    }),
    ("WhatsApp settings snapshot and identity check use read-only phone endpoint", async () =>
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Equal(HttpMethod.Get, request.Method);
            Equal("https://graph.facebook.com/v23.0/123456789?fields=display_phone_number,verified_name", request.RequestUri!.AbsoluteUri);
            Equal(WhatsAppToken, request.Headers.Authorization!.Parameter);
            return Task.FromResult(Json("{\"id\":\"123456789\",\"display_phone_number\":\"+20 10 1234 5678\",\"verified_name\":\"Demo Company\"}"));
        });
        using var client = new HttpClient(handler);
        var settings = new WhatsAppSettings { PhoneNumberId = "123456789" };
        var transport = new WhatsAppTransport(client, settings, WhatsAppToken);
        settings.PhoneNumberId = "987654321";
        var identity = await transport.CheckConnectionAsync(default);
        True(identity.Contains("Demo Company", StringComparison.Ordinal));
        True(identity.Contains("+20 10 1234 5678", StringComparison.Ordinal));
        Equal(1, handler.CallCount);
    }),
    ("WhatsApp HTTP200 Graph error is not accepted and cannot leak secret", async () =>
    {
        using var handler = Respond($"{{\"error\":{{\"code\":190,\"message\":\"{WhatsAppToken}\"}}}}");
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.Authentication, () => WhatsApp(client).SendAsync(WhatsAppMessage(), default));
        Equal(190, ex.ProviderCode);
        False(ex.ToString().Contains(WhatsAppToken, StringComparison.Ordinal));
    }),
    ("WhatsApp HTTP403 is account permissions failure", async () =>
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(Json(
            "{\"error\":{\"code\":200,\"message\":\"Permissions error\"}}", HttpStatusCode.Forbidden)));
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.Authentication, () => WhatsApp(client).SendAsync(WhatsAppMessage(), default));
        Equal(HttpStatusCode.Forbidden, ex.HttpStatusCode);
    }),
    ("WhatsApp HTTP429 exposes retry delay without retrying automatically", async () =>
    {
        using var handler = new FakeHandler((_, _) =>
        {
            var response = Json("{\"error\":{\"code\":130429}}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var ex = await Error(ChannelErrorKind.RateLimited, () => WhatsApp(client).SendAsync(WhatsAppMessage(), default));
        Equal(12, ex.RetryAfterSeconds);
        Equal(130429, ex.ProviderCode);
        Equal(1, handler.CallCount);
    }),
    ("WhatsApp missing message ID or rejected status cannot report accepted", async () =>
    {
        foreach (var body in new[] { "not JSON", "{}", "{\"messaging_product\":\"whatsapp\",\"messages\":[]}",
            "{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"\"}]}",
            "{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.test\",\"message_status\":123}]}",
            "{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.test\",\"message_status\":\"rejected\"}]}" })
        {
            using var handler = Respond(body);
            using var client = new HttpClient(handler);
            await AnyChannelError(() => WhatsApp(client).SendAsync(WhatsAppMessage(), default));
        }
    }),
    ("WhatsApp validates number and template before network", async () =>
    {
        foreach (var recipient in new[] { "", "01012345678", "+ +201012345678", "++201012345678", "2010abc45678" })
        {
            using var handler = NeverCalled();
            using var client = new HttpClient(handler);
            var message = WhatsAppMessage();
            message.Recipient = recipient;
            await Error(ChannelErrorKind.InvalidMessage, () => WhatsApp(client).SendAsync(message, default));
        }
        using var never = NeverCalled();
        using var another = new HttpClient(never);
        var badTemplate = WhatsAppMessage();
        badTemplate.WhatsAppTemplate!.Name = "Not Approved";
        await Error(ChannelErrorKind.InvalidMessage, () => WhatsApp(another).SendAsync(badTemplate, default));
        Equal(0, never.CallCount);
    }),
    ("Invalid token/configuration errors never include supplied values", () =>
    {
        using var client = new HttpClient(NeverCalled());
        foreach (var secret in new[] { "bad/secret\n", "", "token?secret=yes" })
        {
            try { _ = new TelegramTransport(client, secret); throw new Exception("Expected invalid configuration"); }
            catch (ChannelTransportException ex) { Equal(ChannelErrorKind.InvalidConfiguration, ex.Kind); }
        }
        try
        {
            _ = new WhatsAppTransport(client, new WhatsAppSettings { PhoneNumberId = "../escape" }, WhatsAppToken);
            throw new Exception("Expected invalid configuration");
        }
        catch (ChannelTransportException ex)
        {
            Equal(ChannelErrorKind.InvalidConfiguration, ex.Kind);
            False(ex.ToString().Contains(WhatsAppToken, StringComparison.Ordinal));
        }
        return Task.CompletedTask;
    }),
    ("Oversized provider response is rejected before acceptance", async () =>
    {
        using var handler = Respond(new string('x', 1024 * 1024 + 1));
        using var client = new HttpClient(handler);
        await Error(ChannelErrorKind.InvalidResponse, () => WhatsApp(client).SendAsync(WhatsAppMessage(), default));
    })
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}: {ex}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} channel checks passed. All HTTP traffic used fake handlers.");
return failed == 0 ? 0 : 1;

static ChannelMessage TelegramMessage() => new() { Recipient = "12345678", Body = "test" };
static ChannelMessage WhatsAppMessage() => new()
{
    Recipient = "+201012345678", Body = "preview only",
    WhatsAppTemplate = new() { Name = "order_update", LanguageCode = "ar", BodyParameters = ["أحمد", "42"] }
};
static WhatsAppTransport WhatsApp(HttpClient client) => new(client,
    new WhatsAppSettings { PhoneNumberId = "123456789" }, "fake_whatsapp_secret_never_log");
static async Task<JsonDocument> ReadRequest(HttpRequestMessage request, CancellationToken cancellationToken) =>
    JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
static HttpResponseMessage WhatsAppSuccess() =>
    Json("{\"messaging_product\":\"whatsapp\",\"contacts\":[{\"wa_id\":\"201012345678\"}],\"messages\":[{\"id\":\"wamid.test123\"}]}");
static FakeHandler Respond(string body) => new((_, _) => Task.FromResult(Json(body)));
static FakeHandler NeverCalled() => new((_, _) => throw new Exception("No HTTP request should have been made"));
static async Task<ChannelTransportException> TelegramError(HttpResponseMessage response, ChannelErrorKind kind)
{
    using var handler = new FakeHandler((_, _) => Task.FromResult(response));
    using var client = new HttpClient(handler);
    return await Error(kind, () => new TelegramTransport(client, "123456789:fake_bot_test_token")
        .SendAsync(TelegramMessage(), default));
}
static async Task<ChannelTransportException> Error(ChannelErrorKind kind, Func<Task> operation)
{
    var ex = await AnyChannelError(operation);
    Equal(kind, ex.Kind);
    return ex;
}
static async Task<ChannelTransportException> AnyChannelError(Func<Task> operation)
{
    try { await operation(); }
    catch (ChannelTransportException ex) { return ex; }
    throw new Exception("Expected ChannelTransportException");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}");
}
static void True(bool value) { if (!value) throw new Exception("Expected true"); }
static void False(bool value) { if (value) throw new Exception("Expected false"); }

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return handle(request, cancellationToken);
    }
}

sealed class StalledReadStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
}
