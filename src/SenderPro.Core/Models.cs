namespace SenderPro.Core;

public sealed class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Group { get; set; } = "";
    public bool Unsubscribed { get; set; }
}

public sealed class MessageTemplate
{
    public string Name { get; set; } = "قالب جديد";
    public string Subject { get; set; } = "مرحبًا {name}";
    public string Body { get; set; } = "مرحبًا {name}،\n";
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public int DelayMilliseconds { get; set; } = 1000;
}

public sealed class DeliveryResult
{
    public string Recipient { get; set; } = "";
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CampaignRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Mode { get; set; } = "preview";
    public string State { get; set; } = "created";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DeliveryResult> Results { get; set; } = [];
}

public sealed class AppData
{
    public List<Contact> Contacts { get; set; } = [];
    public List<MessageTemplate> Templates { get; set; } = [new()];
    public List<CampaignRecord> Campaigns { get; set; } = [];
    public SmtpSettings Smtp { get; set; } = new();
}

public static class TemplateRenderer
{
    public static string Render(string text, Contact c) => text
        .Replace("{name}", c.Name, StringComparison.Ordinal)
        .Replace("{email}", c.Email, StringComparison.Ordinal)
        .Replace("{phone}", c.Phone, StringComparison.Ordinal)
        .Replace("{group}", c.Group, StringComparison.Ordinal);
}
