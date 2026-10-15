using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SenderPro.Core;

/// <summary>
/// Official WhatsApp Cloud API. Business-initiated messages require an approved template and opt-in.
/// ServiceWindowText is only appropriate inside the provider's open customer-service window;
/// the adapter cannot establish that window locally. No automatic retries are performed.
/// </summary>
public sealed class WhatsAppTransport : ISocialTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _phoneNumberId;
    private readonly string _apiVersion;

    public WhatsAppTransport(HttpClient httpClient, WhatsAppSettings settings, string token)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) || token.Any(char.IsControl))
            throw ChannelHttp.InvalidConfiguration("أدخل رمز وصول WhatsApp Cloud API صالحًا.");
        if (string.IsNullOrWhiteSpace(settings.PhoneNumberId) ||
            !Regex.IsMatch(settings.PhoneNumberId, @"\A[0-9]+\z"))
            throw ChannelHttp.InvalidConfiguration("أدخل Phone Number ID الرقمي من حساب WhatsApp Business.");
        if (string.IsNullOrWhiteSpace(settings.ApiVersion) ||
            !Regex.IsMatch(settings.ApiVersion, @"\Av[1-9][0-9]*\.0\z"))
            throw ChannelHttp.InvalidConfiguration("إصدار Graph API يجب أن يكون مثل v23.0.");
        _httpClient = httpClient;
        _token = token;
        // Snapshot mutable caller settings so later UI edits cannot change an in-progress campaign.
        _phoneNumberId = settings.PhoneNumberId;
        _apiVersion = settings.ApiVersion;
    }

    public async Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(message);
        var recipient = message.Recipient?.Trim() ?? "";
        if (recipient.StartsWith('+')) recipient = recipient[1..];
        if (!Regex.IsMatch(recipient, @"\A[1-9][0-9]{6,14}\z"))
            throw ChannelHttp.InvalidMessage("أدخل رقم WhatsApp بكود الدولة، مثل +201012345678.");
        var payload = new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = recipient
        };
        switch (message.WhatsAppMode)
        {
            case WhatsAppMessageMode.Template:
                payload["type"] = "template";
                payload["template"] = TemplatePayload(message.WhatsAppTemplate);
                break;
            case WhatsAppMessageMode.ServiceWindowText:
                if (string.IsNullOrWhiteSpace(message.Body) || message.Body.EnumerateRunes().Count() > 4096)
                    throw ChannelHttp.InvalidMessage("نص WhatsApp يجب أن يحتوي من حرف إلى 4096 حرفًا.");
                payload["type"] = "text";
                payload["text"] = new { preview_url = false, body = message.Body };
                break;
            default:
                throw ChannelHttp.InvalidMessage("اختر قالب WhatsApp أو وضع نافذة خدمة العميل صراحةً.");
        }

        using var request = Request(HttpMethod.Post, "/messages");
        request.Content = JsonContent.Create(payload);
        using var response = await ChannelHttp.SendAsync(_httpClient, request, false, cancellationToken)
            .ConfigureAwait(false);
        if (ChannelHttp.String(response.RootElement, "messaging_product") != "whatsapp" ||
            !response.RootElement.TryGetProperty("messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() == 0)
            throw ChannelHttp.InvalidResponse();
        var id = ChannelHttp.String(messages[0], "id");
        if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsControl)) throw ChannelHttp.InvalidResponse();
        var providerState = "accepted";
        if (messages[0].TryGetProperty("message_status", out var messageStatus))
        {
            if (messageStatus.ValueKind != JsonValueKind.String) throw ChannelHttp.InvalidResponse();
            if (messageStatus.GetString() is not ("accepted" or "held_for_quality_assessment"))
                throw new ChannelTransportException(ChannelErrorKind.Rejected,
                    "لم تؤكد WhatsApp قبول الرسالة. راجع حالة القالب والحساب.");
            providerState = messageStatus.GetString()!;
        }
        return new ChannelReceipt(ChannelKind.WhatsApp, id, DateTimeOffset.UtcNow, providerState);
    }

    public async Task<string> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = Request(HttpMethod.Get, "?fields=display_phone_number,verified_name");
        using var response = await ChannelHttp.SendAsync(_httpClient, request, false, cancellationToken)
            .ConfigureAwait(false);
        var root = response.RootElement;
        if (ChannelHttp.String(root, "id") != _phoneNumberId) throw ChannelHttp.InvalidResponse();
        var number = ChannelHttp.String(root, "display_phone_number");
        var name = ChannelHttp.String(root, "verified_name");
        var identity = string.Join(" — ", new[] { name, number }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > 512 || identity.Any(char.IsControl))
            throw ChannelHttp.InvalidResponse();
        return identity;
    }

    private static object TemplatePayload(WhatsAppTemplate? template)
    {
        if (template is null || string.IsNullOrWhiteSpace(template.Name) ||
            !Regex.IsMatch(template.Name, @"\A[a-z0-9_]{1,512}\z"))
            throw ChannelHttp.InvalidMessage("اختر اسم قالب WhatsApp معتمدًا باللغة الصحيحة.");
        if (string.IsNullOrWhiteSpace(template.LanguageCode) ||
            !Regex.IsMatch(template.LanguageCode, @"\A[a-z]{2,3}(?:_[A-Z]{2})?\z"))
            throw ChannelHttp.InvalidMessage("أدخل رمز لغة القالب، مثل ar أو en_US.");
        if (template.BodyParameters is null || template.BodyParameters.Any(s =>
                string.IsNullOrWhiteSpace(s) || s.EnumerateRunes().Count() > 32768))
            throw ChannelHttp.InvalidMessage("قيم متغيرات القالب يجب أن تكون نصوصًا غير فارغة.");
        var payload = new Dictionary<string, object>
        {
            ["name"] = template.Name,
            ["language"] = new { code = template.LanguageCode }
        };
        if (template.BodyParameters.Count > 0)
            payload["components"] = new[]
            {
                new { type = "body", parameters = template.BodyParameters.Select(value =>
                    new { type = "text", text = value }).ToArray() }
            };
        return payload;
    }

    private HttpRequestMessage Request(HttpMethod method, string suffix)
    {
        var request = new HttpRequestMessage(method,
            new Uri($"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}{suffix}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }
}
