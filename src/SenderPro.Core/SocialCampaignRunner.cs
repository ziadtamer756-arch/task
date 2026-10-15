using System.Globalization;
using System.Text.RegularExpressions;

namespace SenderPro.Core;

public sealed class SocialCampaignRunner(ISocialTransport transport, ChannelKind kind)
{
    public async Task RunAsync(CampaignRecord campaign, IEnumerable<Contact> contacts, bool preview,
        int delayMilliseconds, Action<DeliveryResult> onResult, CancellationToken token,
        Action? onStarted = null, IReadOnlySet<string>? globalBlockedRecipients = null)
    {
        if (campaign.State != "created") throw new InvalidOperationException("أنشئ حملة جديدة بدل إعادة إرسال حملة سابقة.");
        campaign.Channel = kind == ChannelKind.Telegram ? "telegram" : "whatsapp";
        campaign.Mode = preview ? "preview" : campaign.Channel;
        campaign.State = "running";
        var recipients = contacts.ToList();
        var blocked = recipients.Where(c => c.Unsubscribed).Select(c => RecipientFor(c, kind)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            onStarted?.Invoke();
            foreach (var contact in recipients)
            {
                token.ThrowIfCancellationRequested();
                var recipient = RecipientFor(contact, kind);
                var result = new DeliveryResult { Recipient = recipient };
                var pause = false;
                if (contact.Unsubscribed || blocked.Contains(recipient) || (globalBlockedRecipients?.Contains(recipient) ?? false))
                { result.Status = "skipped"; result.Detail = "إلغاء الاشتراك"; }
                else if (!IsValidRecipient(recipient, kind))
                { result.Status = "skipped"; result.Detail = kind == ChannelKind.Telegram ? "معرّف محادثة تليجرام غير صالح" : "رقم واتساب دولي غير صالح"; }
                else if (!seen.Add(recipient)) { result.Status = "skipped"; result.Detail = "مستلم مكرر في الحملة"; }
                else if (preview) { result.Status = "preview"; result.Detail = "معاينة محلية — لم يتم الإرسال"; }
                else
                {
                    var message = new ChannelMessage { Recipient = recipient, Body = TemplateRenderer.Render(campaign.Body, contact),
                        WhatsAppMode = campaign.WhatsAppMode,
                        WhatsAppTemplate = campaign.WhatsAppTemplate is null ? null : new WhatsAppTemplate
                        {
                            Name = campaign.WhatsAppTemplate.Name, LanguageCode = campaign.WhatsAppTemplate.LanguageCode,
                            BodyParameters = campaign.WhatsAppTemplate.BodyParameters.Select(p => TemplateRenderer.Render(p, contact)).ToList()
                        } };
                    try
                    {
                        var receipt = await transport.SendAsync(message, token);
                        result.ProviderMessageId = receipt.MessageId; result.At = receipt.AcceptedAt;
                        var held = receipt.ProviderState == "held_for_quality_assessment";
                        result.Status = held ? "held" : "accepted";
                        result.Detail = held ? "الرسالة معلقة لدى واتساب لمراجعة الجودة؛ لم يتأكد إرسالها" : "قبل المزود الرسالة؛ التسليم والقراءة غير مؤكدين";
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    { result.Status = "unknown"; result.Detail = "انتهت مهلة الاتصال؛ تحقق من النتيجة لدى المزود قبل إعادة المحاولة"; }
                    catch (OperationCanceledException)
                    {
                        result.Status = "unknown"; result.Detail = "أُلغي الطلب أثناء الإرسال؛ حالة الرسالة لدى المزود غير مؤكدة";
                        campaign.Results.Add(result); onResult(result); throw;
                    }
                    catch (ChannelTransportException ex)
                    {
                        result.Status = ex.Kind is ChannelErrorKind.Network or ChannelErrorKind.InvalidResponse ? "unknown" : "failed"; result.Detail = ex.Message;
                        if (ex.ProviderCode is { } code) result.Detail += $" — رمز المزود: {code}";
                        pause = ex.Kind is ChannelErrorKind.Authentication or ChannelErrorKind.RateLimited or ChannelErrorKind.InvalidConfiguration;
                        if (ex.RetryAfterSeconds is > 0) result.Detail += $" — مهلة المزود: {ex.RetryAfterSeconds} ثانية";
                    }
                    catch (HttpRequestException)
                    { result.Status = "unknown"; result.Detail = "انقطع الاتصال؛ تحقق من حالة الرسالة لدى المزود قبل إعادة المحاولة"; }
                    catch (Exception)
                    { result.Status = "failed"; result.Detail = "تعذر إتمام الطلب؛ تحقق من الإعدادات وحالة الحساب"; }
                }
                campaign.Results.Add(result); onResult(result);
                if (preview && campaign.Results.Count % 64 == 0) await Task.Yield();
                if (pause) { campaign.State = "paused"; return; }
                if (!preview && result.Status is "accepted" or "failed" or "held" or "unknown")
                    await Task.Delay(Math.Max(0, delayMilliseconds), token);
            }
            campaign.State = "completed";
        }
        catch (OperationCanceledException) { campaign.State = "cancelled"; }
        catch { campaign.State = "failed"; throw; }
    }

    public static string RecipientFor(Contact contact, ChannelKind channel)
    {
        var value = channel == ChannelKind.Telegram ? contact.TelegramChatId.Trim() : contact.Phone.Trim();
        if (channel == ChannelKind.Telegram) return value;
        value = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        return value.StartsWith('+') ? value[1..] : value;
    }

    public static bool IsValidRecipient(string value, ChannelKind channel) => channel == ChannelKind.Telegram
        ? (Regex.IsMatch(value, "\\A-?[1-9][0-9]*\\z") && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id) && id != 0) ||
            Regex.IsMatch(value, "\\A@[A-Za-z][A-Za-z0-9_]{4,31}\\z")
        : Regex.IsMatch(value, "\\A[1-9][0-9]{6,14}\\z");
}
