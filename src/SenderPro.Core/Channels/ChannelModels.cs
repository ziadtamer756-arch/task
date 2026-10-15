using System.Net;

namespace SenderPro.Core;

public enum ChannelKind { Telegram, WhatsApp }

/// <summary>Minimal chat metadata from the user's bot updates; no message content is retained.</summary>
public sealed record TelegramChat(string Id, string Name, string Type);

/// <summary>Template is the default; callers must deliberately choose service-window text.</summary>
public enum WhatsAppMessageMode { Template, ServiceWindowText }

public sealed class ChannelMessage
{
    public string Recipient { get; set; } = "";
    public string Body { get; set; } = "";
    public WhatsAppTemplate? WhatsAppTemplate { get; set; }
    public WhatsAppMessageMode WhatsAppMode { get; set; } = WhatsAppMessageMode.Template;
}

public sealed class WhatsAppTemplate
{
    public string Name { get; set; } = "";
    public string LanguageCode { get; set; } = "en_US";
    public List<string> BodyParameters { get; set; } = [];
}

/// <summary>Nonsecret configuration. Access tokens belong only in the transport constructor.</summary>
public sealed class WhatsAppSettings
{
    public string PhoneNumberId { get; set; } = "";
    public string ApiVersion { get; set; } = "v23.0";
}

/// <summary>
/// The provider returned a message ID. ProviderState may indicate a quality-assessment hold;
/// neither acceptance nor a hold establishes delivery or reading by the recipient.
/// </summary>
public sealed record ChannelReceipt(ChannelKind Channel, string MessageId, DateTimeOffset AcceptedAt,
    string ProviderState = "accepted");

public interface ISocialTransport
{
    Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken cancellationToken);
    Task<string> CheckConnectionAsync(CancellationToken cancellationToken);
}

public enum ChannelErrorKind
{
    InvalidConfiguration,
    InvalidMessage,
    Network,
    Authentication,
    RateLimited,
    Rejected,
    InvalidResponse
}

/// <summary>
/// Does not retain raw responses, request URIs, tokens, or inner HTTP exceptions.
/// Telegram includes the credential in its URL, so forwarding those exceptions is unsafe.
/// </summary>
public sealed class ChannelTransportException : Exception
{
    public ChannelErrorKind Kind { get; }
    public HttpStatusCode? HttpStatusCode { get; }
    public int? ProviderCode { get; }
    public int? RetryAfterSeconds { get; }

    public ChannelTransportException(ChannelErrorKind kind, string message,
        HttpStatusCode? httpStatusCode = null, int? providerCode = null, int? retryAfterSeconds = null)
        : base(message)
    {
        Kind = kind;
        HttpStatusCode = httpStatusCode;
        ProviderCode = providerCode;
        RetryAfterSeconds = retryAfterSeconds;
    }
}
