using System.Net;
using System.Net.Mail;

namespace SenderPro.Core;

public interface IEmailTransport
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken token);
}

public sealed class SmtpTransport(SmtpSettings settings, string password, IReadOnlyList<string>? attachments = null) : IEmailTransport
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableTls,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, password);
        using var message = new MailMessage(settings.FromAddress, recipient, subject, body);
        foreach (var path in attachments ?? []) message.Attachments.Add(new Attachment(path));
        await client.SendMailAsync(message, token);
    }
}

public sealed class CampaignRunner(IEmailTransport transport)
{
    public async Task RunAsync(CampaignRecord campaign, IEnumerable<Contact> contacts,
        bool preview, int delayMilliseconds, Action<DeliveryResult> onResult, CancellationToken token,
        Action? onStarted = null, IReadOnlySet<string>? globalBlockedRecipients = null)
    {
        if (campaign.State != "created") throw new InvalidOperationException("أنشئ حملة جديدة بدل إعادة إرسال حملة سابقة.");
        campaign.State = "running";
        campaign.Mode = preview ? "preview" : "smtp";
        var recipients = contacts.ToList();
        var unsubscribed = recipients.Where(c => c.Unsubscribed)
            .Select(c => c.Email.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            onStarted?.Invoke();
            foreach (var c in recipients)
            {
                token.ThrowIfCancellationRequested();
                var email = c.Email.Trim();
                var result = new DeliveryResult { Recipient = email };
                if (c.Unsubscribed || unsubscribed.Contains(email) || (globalBlockedRecipients?.Contains(email) ?? false)) { result.Status = "skipped"; result.Detail = "إلغاء الاشتراك"; }
                else if (!IsEmail(email)) { result.Status = "skipped"; result.Detail = "عنوان بريد غير صالح"; }
                else if (!seen.Add(email)) { result.Status = "skipped"; result.Detail = "عنوان مكرر في الحملة"; }
                else if (preview) { result.Status = "preview"; result.Detail = "معاينة محلية — لم يتم الإرسال"; }
                else
                {
                    try
                    {
                        await transport.SendAsync(email, TemplateRenderer.Render(campaign.Subject, c),
                            TemplateRenderer.Render(campaign.Body, c), token);
                        result.Status = "accepted";
                        result.Detail = "قبله خادم SMTP؛ وصوله إلى صندوق المستلم غير مؤكد";
                    }
                    catch (OperationCanceledException)
                    {
                        result.Status = "unknown"; result.Detail = "أُلغي الطلب أثناء الإرسال؛ تحقق من النتيجة لدى الخادم قبل إعادة المحاولة";
                        campaign.Results.Add(result); onResult(result); throw;
                    }
                    catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException)
                    { result.Status = "failed"; result.Detail = ex.Message; }
                }
                campaign.Results.Add(result);
                onResult(result);
                if (preview && campaign.Results.Count % 64 == 0) await Task.Yield();
                if (!preview && result.Status is "accepted" or "failed")
                    await Task.Delay(Math.Max(0, delayMilliseconds), token);
            }
            campaign.State = "completed";
        }
        catch (OperationCanceledException) { campaign.State = "cancelled"; }
        catch { campaign.State = "failed"; throw; }
    }

    public static bool IsEmail(string value) => !string.IsNullOrWhiteSpace(value) &&
        MailAddress.TryCreate(value, out var parsed) && parsed.Address == value &&
        parsed.Host.Contains('.');
}
