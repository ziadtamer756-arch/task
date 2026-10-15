using System.Net;
using System.Text.Json;

namespace SenderPro.Core;

internal static class ChannelHttp
{
    // Response bodies are small for these endpoints. Bound memory use on unexpected responses.
    private const int MaximumResponseBytes = 1024 * 1024;

    internal static ChannelTransportException InvalidConfiguration(string message) =>
        new(ChannelErrorKind.InvalidConfiguration, message);

    internal static ChannelTransportException InvalidMessage(string message) =>
        new(ChannelErrorKind.InvalidMessage, message);

    internal static ChannelTransportException InvalidResponse() =>
        new(ChannelErrorKind.InvalidResponse, "الخدمة أعادت استجابة غير مكتملة؛ لم يتم تأكيد قبول الرسالة.");

    internal static async Task<JsonDocument> SendAsync(HttpClient client, HttpRequestMessage request,
        bool telegram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // ResponseHeadersRead otherwise limits HttpClient.Timeout to the headers phase only.
        // Apply the configured deadline to response-body reads as well.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (client.Timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(client.Timeout);
        var operationToken = deadline.Token;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                operationToken).ConfigureAwait(false);
            JsonDocument? document = null;
            try
            {
                var bytes = await ReadBoundedAsync(response.Content, operationToken).ConfigureAwait(false);
                try { document = JsonDocument.Parse(bytes); }
                catch (JsonException) { /* HTTP errors may have a non-JSON body. */ }

                var root = document?.RootElement ?? default;
                var hasProviderError = root.ValueKind == JsonValueKind.Object &&
                    (telegram
                        ? !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                        : root.TryGetProperty("error", out _));
                if (!response.IsSuccessStatusCode || hasProviderError)
                    throw ProviderError(root, response, telegram);
                if (document is null || root.ValueKind != JsonValueKind.Object)
                    throw InvalidResponse();

                var result = document;
                document = null;
                return result;
            }
            finally { document?.Dispose(); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChannelTransportException(ChannelErrorKind.Network,
                "انتهت مهلة الاتصال بالخدمة. تحقق من حالة الرسالة قبل إعادة المحاولة.");
        }
        catch (OperationCanceledException)
        {
            // Preserve cancellation semantics without forwarding an exception that might contain a URI.
            throw new OperationCanceledException("تم إلغاء الاتصال بالخدمة.", cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Do not propagate Message/InnerException: the Telegram URI contains the bot token.
            throw new ChannelTransportException(ChannelErrorKind.Network,
                "تعذر الاتصال بالخدمة. تحقق من الإنترنت وحالة الرسالة قبل إعادة المحاولة.");
        }
        catch (IOException)
        {
            throw new ChannelTransportException(ChannelErrorKind.Network,
                "انقطع الاتصال أثناء قراءة الاستجابة. تحقق من حالة الرسالة قبل إعادة المحاولة.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes) throw InvalidResponse();
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (output.Length + count > MaximumResponseBytes) throw InvalidResponse();
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static ChannelTransportException ProviderError(JsonElement root, HttpResponseMessage response,
        bool telegram)
    {
        var error = root;
        if (!telegram && root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var nested))
            error = nested;
        var code = Number(error, telegram ? "error_code" : "code");
        int? retryAfter = null;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parameters", out var parameters))
            retryAfter = Number(parameters, "retry_after");
        if (retryAfter is null && response.Headers.RetryAfter?.Delta is { } delta)
            retryAfter = (int)Math.Clamp(Math.Ceiling(delta.TotalSeconds), 0, int.MaxValue);
        if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
            retryAfter = (int)Math.Clamp(Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds), 0, int.MaxValue);
        if (retryAfter < 0) retryAfter = null;

        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ChannelErrorKind.Authentication,
            // Telegram uses 403 for a blocked bot / inaccessible recipient, not an invalid token.
            // That recipient-specific failure must not halt delivery to unrelated chats.
            HttpStatusCode.Forbidden when !telegram => ChannelErrorKind.Authentication,
            HttpStatusCode.Forbidden => ChannelErrorKind.Rejected,
            HttpStatusCode.TooManyRequests => ChannelErrorKind.RateLimited,
            _ when code is 401 => ChannelErrorKind.Authentication,
            _ when !telegram && code is 403 or 190 or 102 => ChannelErrorKind.Authentication,
            _ when code is 429 or 4 or 17 or 80007 or 130429 or 131048 or 131056 => ChannelErrorKind.RateLimited,
            _ => ChannelErrorKind.Rejected
        };
        var message = kind switch
        {
            _ when telegram && (response.StatusCode == HttpStatusCode.Conflict || code == 409) =>
                "تعذر قراءة تحديثات البوت: قد يوجد Webhook أو برنامج آخر يقرأ التحديثات. أغلق جلسة القراءة الأخرى أو راجع التكامل الحالي.",
            ChannelErrorKind.Authentication => "رفضت الخدمة المصادقة أو الصلاحيات. راجع الرمز وصلاحيات الحساب.",
            ChannelErrorKind.RateLimited => "تم بلوغ حد الإرسال لدى الخدمة. انتظر قبل محاولة جديدة.",
            _ => "رفضت الخدمة الطلب. راجع المستلم والقالب وإعدادات الحساب ورمز الخطأ."
        };
        // Provider descriptions can contain credentials or message content. Keep only numeric metadata.
        return new ChannelTransportException(kind, message, response.StatusCode, code, retryAfter);
    }

    internal static int? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    internal static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
