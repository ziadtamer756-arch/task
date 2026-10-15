using System.ComponentModel;
using System.Text.RegularExpressions;
using SenderPro.Core;

namespace SenderPro.Windows;

public sealed partial class MainForm
{
    private string telegramToken = "";
    private string whatsappToken = "";
    private ComboBox? smokeChannel;
    private Func<Task>? smokePreview;
    private bool uiSmokeRunning;

    private static HttpClient CreateChannelClient(int seconds = 30) => new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(seconds) };

    private void MessagingAccounts()
    {
        var page = Page("ربط الحسابات", "أدخل بيانات حسابك هنا. الرموز السرية تبقى في ذاكرة البرنامج لهذه الجلسة؛ تُحفظ إعدادات القنوات غير السرية فقط.");
        var tgToken = Field(page, "Telegram — رمز البوت", telegramToken); tgToken.UseSystemPasswordChar = true;
        var tgInfo = new Label { Text = string.IsNullOrWhiteSpace(data.Messaging.TelegramBotUsername) ? "البوت يرسل إلى محادثات بدأت التواصل معه أو مجموعات أُضيف إليها." : "آخر بوت تم التحقق منه: @" + data.Messaging.TelegramBotUsername,
            AutoSize = true, ForeColor = Theme.Muted }; page.Controls.Add(tgInfo);
        var tgTest = Button("اختبار اتصال تليجرام", (_, _) => { }); page.Controls.Add(tgTest);
        var tgChats = Button("استيراد محادثات البوت الحديثة", (_, _) => { }); page.Controls.Add(tgChats);
        var waPhone = Field(page, "WhatsApp — Phone number ID", data.Messaging.WhatsAppPhoneNumberId);
        var waVersion = Field(page, "إصدار Graph API", data.Messaging.WhatsAppApiVersion);
        var waToken = Field(page, "WhatsApp — Access token", whatsappToken); waToken.UseSystemPasswordChar = true;
        var waInfo = new Label { Text = "يتطلب حساب WhatsApp Business ورقمًا مرتبطًا بـ Cloud API. افحص الاتصال ثم استخدم قالبًا معتمدًا لبدء الرسائل.", AutoSize = true, ForeColor = Theme.Muted }; page.Controls.Add(waInfo);
        var waTest = Button("اختبار اتصال واتساب", (_, _) => { }); page.Controls.Add(waTest);
        var template = Field(page, "اسم قالب واتساب الافتراضي", data.Messaging.WhatsAppTemplateName);
        var language = Field(page, "لغة القالب مثل ar أو en_US", data.Messaging.WhatsAppLanguageCode);
        var delay = Field(page, "التأخير بين الرسائل بالمللي ثانية", data.Messaging.DelayMilliseconds.ToString());
        var show = new CheckBox { Text = "إظهار الرموز السرية", AutoSize = true };
        show.CheckedChanged += (_, _) => tgToken.UseSystemPasswordChar = waToken.UseSystemPasswordChar = !show.Checked;
        page.Controls.Add(show);
        void SaveSettings()
        {
            if (!Regex.IsMatch(waVersion.Text.Trim(), "^v[0-9]+\\.[0-9]+$")) throw new InvalidDataException("صيغة إصدار Graph API مثل v23.0.");
            if (!string.IsNullOrWhiteSpace(waPhone.Text) && !waPhone.Text.Trim().All(char.IsAsciiDigit)) throw new InvalidDataException("Phone number ID يتكون من أرقام فقط.");
            if (!int.TryParse(delay.Text, out var milliseconds) || milliseconds < 0 || milliseconds > 3600000)
                throw new InvalidDataException("التأخير بين 0 و3600000 مللي ثانية.");
            telegramToken = tgToken.Text.Trim(); whatsappToken = waToken.Text.Trim();
            data.Messaging.WhatsAppPhoneNumberId = waPhone.Text.Trim(); data.Messaging.WhatsAppApiVersion = waVersion.Text.Trim();
            data.Messaging.WhatsAppTemplateName = template.Text.Trim(); data.Messaging.WhatsAppLanguageCode = language.Text.Trim();
            data.Messaging.DelayMilliseconds = milliseconds; Save();
        }
        tgTest.Click += async (_, _) =>
        {
            tgTest.Enabled = false;
            try
            {
                using var client = CreateChannelClient(20);
                var username = await new TelegramTransport(client, tgToken.Text.Trim()).CheckConnectionAsync(CancellationToken.None);
                if (tgInfo.IsDisposed) return;
                data.Messaging.TelegramBotUsername = username.TrimStart('@'); SaveSettings(); tgInfo.Text = "الاتصال ناجح: " + username;
            }
            catch (ChannelTransportException ex) { if (!tgInfo.IsDisposed) tgInfo.Text = ex.Message; }
            catch { if (!tgInfo.IsDisposed) tgInfo.Text = "تعذر التحقق من الاتصال. راجع الرمز والشبكة والإعدادات."; }
            finally { if (!tgTest.IsDisposed) tgTest.Enabled = true; }
        };
        tgChats.Click += async (_, _) =>
        {
            tgChats.Enabled = false;
            try
            {
                using var client = CreateChannelClient(20);
                var chats = await new TelegramTransport(client, tgToken.Text.Trim()).GetRecentChatsAsync(CancellationToken.None);
                if (tgInfo.IsDisposed) return;
                var imported = chats.Select(c => new Contact { Name = c.Name, TelegramChatId = c.Id, Group = "محادثات تليجرام" });
                var count = ContactCsv.Merge(data.Contacts, imported); telegramToken = tgToken.Text.Trim(); Save();
                tgInfo.Text = chats.Count == 0 ? "لا توجد تحديثات حديثة متاحة. ابدأ محادثة مع البوت ثم جرّب مجددًا." : $"أُضيفت {count} جهة اتصال من {chats.Count} محادثة حديثة؛ تم دمج المكرر.";
            }
            catch (ChannelTransportException ex) { if (!tgInfo.IsDisposed) tgInfo.Text = ex.Message; }
            catch { if (!tgInfo.IsDisposed) tgInfo.Text = "تعذر استيراد المحادثات. تحقق من الرمز والشبكة وإمكانية حفظ البيانات."; }
            finally { if (!tgChats.IsDisposed) tgChats.Enabled = true; }
        };
        waTest.Click += async (_, _) =>
        {
            waTest.Enabled = false;
            try
            {
                using var client = CreateChannelClient(20);
                var transport = new WhatsAppTransport(client, new WhatsAppSettings { PhoneNumberId = waPhone.Text.Trim(), ApiVersion = waVersion.Text.Trim() }, waToken.Text.Trim());
                var identity = await transport.CheckConnectionAsync(CancellationToken.None);
                if (waInfo.IsDisposed) return;
                SaveSettings(); waInfo.Text = "الاتصال ناجح: " + identity;
            }
            catch (ChannelTransportException ex) { if (!waInfo.IsDisposed) waInfo.Text = ex.Message; }
            catch { if (!waInfo.IsDisposed) waInfo.Text = "تعذر التحقق من الاتصال. راجع رقم الحساب والرمز والإصدار والصلاحيات."; }
            finally { if (!waTest.IsDisposed) waTest.Enabled = true; }
        };
        page.Controls.Add(Button("حفظ إعدادات القنوات", (_, _) => Guard(() => { SaveSettings(); status.Text = "تم حفظ الإعدادات. رموز الدخول محفوظة لهذه الجلسة فقط."; })));
    }

    private void Campaign()
    {
        var page = Page("حملة جديدة", "اختر القناة والجمهور، راجع المعاينة ثم ابدأ. يتم استبعاد التكرار وإلغاء الاشتراك عبر جميع المجموعات.");
        var channel = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, Name = "campaign-channel" };
        channel.Items.AddRange(["البريد الإلكتروني — SMTP", "تليجرام — Bot API", "واتساب — Cloud API"]); channel.SelectedIndex = 0;
        page.Controls.Add(channel); smokeChannel = channel;
        var name = Field(page, "اسم الحملة", "حملة " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        var group = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList };
        group.Items.Add("كل المجموعات"); group.Items.AddRange(data.Contacts.Select(c => c.Group).Where(g => g.Length > 0).Distinct().Cast<object>().ToArray()); group.SelectedIndex = 0; page.Controls.Add(group);
        var template = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        template.Items.AddRange(data.Templates.Cast<object>().ToArray()); page.Controls.Add(template);
        var subject = Field(page, "عنوان البريد", "مرحبًا {name}");
        var body = Field(page, "نص الرسالة", "مرحبًا {name}،\r\n"); body.Multiline = true; body.Height = 120; body.ScrollBars = ScrollBars.Vertical;
        template.SelectedIndexChanged += (_, _) => { if (template.SelectedItem is MessageTemplate t) { subject.Text = t.Subject; body.Text = t.Body; } };
        if (template.Items.Count > 0) template.SelectedIndex = 0;
        var waMode = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, Name = "whatsapp-mode" };
        waMode.Items.AddRange(["واتساب: قالب معتمد لبدء الرسائل", "واتساب: نص داخل نافذة خدمة العميل"]); waMode.SelectedIndex = 0; page.Controls.Add(waMode);
        var waName = Field(page, "اسم قالب واتساب المعتمد", data.Messaging.WhatsAppTemplateName);
        var waLanguage = Field(page, "لغة القالب المعتمد", data.Messaging.WhatsAppLanguageCode);
        var waParameters = Field(page, "قيم متغيرات نص القالب بالترتيب — قيمة في كل سطر", "");
        waParameters.Multiline = true; waParameters.Height = 85; waParameters.ScrollBars = ScrollBars.Vertical;
        var hint = new Label { AutoSize = true, ForeColor = Theme.Muted }; page.Controls.Add(hint);
        var attachments = new List<string>();
        var attachmentInfo = new Label { Text = "لا توجد مرفقات", AutoSize = true, ForeColor = Theme.Muted };
        var attachmentBar = Toolbar();
        attachmentBar.Controls.Add(Button("إضافة مرفقات بريد", (_, _) => Guard(() =>
        {
            using var file = new OpenFileDialog { Multiselect = true, Title = "مرفقات الحملة" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            foreach (var path in file.FileNames) if (!attachments.Contains(path)) attachments.Add(path);
            attachmentInfo.Text = "المرفقات: " + string.Join("، ", attachments.Select(Path.GetFileName));
        })));
        attachmentBar.Controls.Add(Button("إزالة المرفقات", (_, _) => { attachments.Clear(); attachmentInfo.Text = "لا توجد مرفقات"; }));
        page.Controls.Add(attachmentBar); page.Controls.Add(attachmentInfo);
        var libraryItems = data.ContentItems.OrderByDescending(c => c.UpdatedAt).ToList();
        var library = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList };
        library.Items.Add("اختر محتوى محفوظًا من المكتبة"); library.Items.AddRange(libraryItems.Select(c => (object)c.Name).ToArray()); library.SelectedIndex = 0; page.Controls.Add(library);
        var applyContent = Button("تطبيق المحتوى المختار", (_, _) =>
        {
            if (library.SelectedIndex < 1) return;
            var saved = libraryItems[library.SelectedIndex - 1]; body.Text = saved.Text;
            if (channel.SelectedIndex == 0)
            {
                attachments.Clear(); attachments.AddRange(saved.MediaPaths);
                attachmentInfo.Text = attachments.Count == 0 ? "لا توجد مرفقات" : "المرفقات: " + string.Join("، ", attachments.Select(Path.GetFileName));
            }
        }); page.Controls.Add(applyContent);
        var sample = Field(page, "معاينة لأول جهة اتصال في المجموعة", ""); sample.Multiline = true; sample.ReadOnly = true; sample.Height = 125; sample.ScrollBars = ScrollBars.Vertical;
        var progress = new Label { AutoSize = true, Text = "جاهز", ForeColor = Theme.Accent };
        var results = Grid(); results.Height = 230; results.ReadOnly = true; results.Name = "campaign-results";
        Column(results, "Recipient", "المستلم"); Column(results, "Status", "الحالة"); Column(results, "Detail", "التفاصيل"); Column(results, "ProviderMessageId", "معرّف المزود");
        var displayed = new BindingList<DeliveryResult>(); results.DataSource = displayed;
        var bar = Toolbar();
        var preview = Button("معاينة الحملة", (_, _) => { });
        var send = Button("إرسال الحملة", (_, _) => { }); send.BackColor = Theme.Accent; send.ForeColor = Color.White;
        var stop = Button("إيقاف", (_, _) => cancellation?.Cancel()); stop.Enabled = false; stop.Name = "campaign-stop";
        preview.Click += async (_, _) => await Run(true); send.Click += async (_, _) => await Run(false);
        smokePreview = () => Run(true);
        bar.Controls.Add(preview); bar.Controls.Add(send); bar.Controls.Add(stop); page.Controls.Add(bar); page.Controls.Add(progress); page.Controls.Add(results);
        string[] Parameters() => waParameters.Text.Length == 0 ? [] : waParameters.Text.Replace("\r", "").Split('\n');
        void UpdateSample()
        {
            var c = data.Contacts.FirstOrDefault(c => !c.Unsubscribed && (group.SelectedIndex == 0 || c.Group == (string?)group.SelectedItem));
            if (c is null) { sample.Text = "أضف جهة اتصال للمجموعة حتى تظهر المعاينة."; return; }
            var recipient = channel.SelectedIndex == 0 ? c.Email : SocialCampaignRunner.RecipientFor(c, channel.SelectedIndex == 1 ? ChannelKind.Telegram : ChannelKind.WhatsApp);
            var text = channel.SelectedIndex == 2 && waMode.SelectedIndex == 0
                ? $"القالب: {waName.Text} | اللغة: {waLanguage.Text}\r\n" + string.Join("\r\n", Parameters().Select((p, i) => $"{{{{{i + 1}}}}}: " + TemplateRenderer.Render(p, c)))
                : (channel.SelectedIndex == 0 ? TemplateRenderer.Render(subject.Text, c) + "\r\n\r\n" : "") + TemplateRenderer.Render(body.Text, c);
            sample.Text = "إلى: " + recipient + "\r\n" + text;
        }
        void ConfigureChannel()
        {
            var whatsapp = channel.SelectedIndex == 2;
            var approvedTemplate = whatsapp && waMode.SelectedIndex == 0;
            library.Visible = applyContent.Visible = !approvedTemplate;
            SetFieldVisible(subject, channel.SelectedIndex == 0); SetFieldVisible(body, !approvedTemplate);
            template.Visible = !approvedTemplate; waMode.Visible = whatsapp;
            SetFieldVisible(waName, approvedTemplate); SetFieldVisible(waLanguage, approvedTemplate); SetFieldVisible(waParameters, approvedTemplate);
            attachmentBar.Visible = attachmentInfo.Visible = channel.SelectedIndex == 0;
            hint.Text = channel.SelectedIndex switch
            {
                0 => "مرفقات البريد تُرسل كما هي. القبول من الخادم لا يؤكد التسليم.",
                1 => "استخدم Telegram chat ID. البوت لا يستطيع بدء محادثة مع مستخدم لم يبدأ التواصل معه. تُطبق نصوص المكتبة فقط؛ مرفقاتها متاحة بالبريد.",
                _ => approvedTemplate ? "اسم القالب ولغته وعدد قيمه يجب أن يطابقوا القالب المعتمد في حساب Business. المعاينة تعرض القيم؛ نص القالب محفوظ لدى واتساب." : "النص الحر متاح داخل نافذة خدمة العميل فقط. تُطبق نصوص المكتبة؛ المرفقات متاحة بالبريد."
            };
            UpdateSample(); ((ResponsivePage)page).Fit();
        }
        channel.SelectedIndexChanged += (_, _) => ConfigureChannel(); waMode.SelectedIndexChanged += (_, _) => ConfigureChannel();
        group.SelectedIndexChanged += (_, _) => UpdateSample(); subject.TextChanged += (_, _) => UpdateSample(); body.TextChanged += (_, _) => UpdateSample();
        waName.TextChanged += (_, _) => UpdateSample(); waLanguage.TextChanged += (_, _) => UpdateSample(); waParameters.TextChanged += (_, _) => UpdateSample(); ConfigureChannel();

        async Task Run(bool previewOnly)
        {
            if (cancellation is not null) return;
            CampaignRecord? record = null;
            using var client = CreateChannelClient();
            var controls = new Control[] { channel, name, group, template, subject, body, waMode, waName, waLanguage, waParameters, attachmentBar, library, applyContent };
            try
            {
                var channelId = channel.SelectedIndex switch { 1 => "telegram", 2 => "whatsapp", _ => "smtp" };
                var approvedTemplate = channelId == "whatsapp" && waMode.SelectedIndex == 0;
                if (string.IsNullOrWhiteSpace(name.Text) || (channelId == "smtp" && string.IsNullOrWhiteSpace(subject.Text)) || (!approvedTemplate && string.IsNullOrWhiteSpace(body.Text) && !(channelId == "smtp" && attachments.Count > 0)))
                    throw new InvalidDataException("أكمل اسم الحملة ومحتوى الرسالة.");
                if (approvedTemplate && (!Regex.IsMatch(waName.Text.Trim(), "^[a-z0-9_]+$") || !Regex.IsMatch(waLanguage.Text.Trim(), "^[a-z]{2,3}(_[A-Z]{2})?$")))
                    throw new InvalidDataException("أدخل اسم القالب المعتمد ورمز لغته بشكل صحيح.");
                var selected = data.Contacts.Where(c => group.SelectedIndex == 0 || c.Group == (string?)group.SelectedItem).Select(CloneContact).ToList();
                if (selected.Count == 0) throw new InvalidDataException("لا توجد جهات اتصال في المجموعة المحددة.");
                ISocialTransport? social = null;
                if (!previewOnly)
                {
                    if (channelId == "smtp")
                    {
                        ValidateSmtp(); if (attachments.Any(path => !File.Exists(path))) throw new InvalidDataException("أحد المرفقات لم يعد موجودًا.");
                    }
                    else if (channelId == "telegram") social = new TelegramTransport(client, telegramToken);
                    else social = new WhatsAppTransport(client, new WhatsAppSettings { PhoneNumberId = data.Messaging.WhatsAppPhoneNumberId, ApiVersion = data.Messaging.WhatsAppApiVersion }, whatsappToken);
                    if (MessageBox.Show($"إرسال الحملة عبر {FriendlyState(channelId)}؟\n{selected.Count} سجلًا قبل التصفية.", "إرسال الحملة", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                }
                record = new CampaignRecord { Name = name.Text, Subject = channelId == "smtp" ? subject.Text : "", Body = approvedTemplate ? "" : body.Text,
                    Channel = channelId, Mode = previewOnly ? "preview" : channelId, Attachments = channelId == "smtp" ? attachments.ToList() : [],
                    WhatsAppMode = waMode.SelectedIndex == 0 ? WhatsAppMessageMode.Template : WhatsAppMessageMode.ServiceWindowText,
                    WhatsAppTemplate = approvedTemplate ? new WhatsAppTemplate { Name = waName.Text.Trim(), LanguageCode = waLanguage.Text.Trim(), BodyParameters = Parameters().ToList() } : null };
                data.Campaigns.Add(record); Save(); displayed.Clear(); cancellation = new CancellationTokenSource();
                foreach (var control in controls) control.Enabled = false;
                preview.Enabled = send.Enabled = navigation.Enabled = false; stop.Enabled = true;
                void Progress(DeliveryResult result)
                {
                    displayed.Add(result);
                    if (!previewOnly) Save();
                    if (!previewOnly || record.Results.Count % 64 == 0)
                        progress.Text = $"تمت معالجة {record.Results.Count} من {selected.Count}";
                }
                if (channelId == "smtp")
                {
                    var blocked = data.Contacts.Where(c => c.Unsubscribed).Select(c => c.Email.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    await new CampaignRunner(new SmtpTransport(data.Smtp, smtpPassword, record.Attachments)).RunAsync(record, selected, previewOnly, data.Smtp.DelayMilliseconds, Progress, cancellation.Token, Save, blocked);
                }
                else
                {
                    var kind = channelId == "telegram" ? ChannelKind.Telegram : ChannelKind.WhatsApp;
                    var blocked = data.Contacts.Where(c => c.Unsubscribed).Select(c => SocialCampaignRunner.RecipientFor(c, kind)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    // Preview never calls this transport; no credential is required to inspect the campaign.
                    social ??= new PreviewOnlyTransport();
                    await new SocialCampaignRunner(social, kind).RunAsync(record, selected, previewOnly, data.Messaging.DelayMilliseconds, Progress, cancellation.Token, Save, blocked);
                }
                progress.Text = $"{FriendlyState(record.State)} — تمت معالجة {record.Results.Count} سجلًا";
            }
            catch (ChannelTransportException ex) { if (record is not null) record.State = "failed"; ShowCampaignError(ex.Message); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            { if (record is not null) record.State = "failed"; ShowCampaignError(ex.Message); }
            catch { if (record is not null) record.State = "failed"; ShowCampaignError("تعذر إتمام الحملة. تحقق من إعدادات الاتصال وإمكانية حفظ البيانات."); }
            finally
            {
                cancellation?.Dispose(); cancellation = null;
                foreach (var control in controls) control.Enabled = true;
                preview.Enabled = send.Enabled = navigation.Enabled = true; stop.Enabled = false; Guard(Save);
            }
        }
    }

    private void ShowCampaignError(string text)
    {
        if (uiSmokeRunning) throw new InvalidOperationException(text);
        MessageBox.Show(this, text, "تعذر تشغيل الحملة", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static Contact CloneContact(Contact c) => new() { Id = c.Id, Name = c.Name, Email = c.Email,
        Phone = c.Phone, TelegramChatId = c.TelegramChatId, Group = c.Group, Unsubscribed = c.Unsubscribed };

    private static void SetFieldVisible(TextBox field, bool visible)
    {
        field.Visible = visible;
        if (field.Parent is not { } parent) return;
        var index = parent.Controls.IndexOf(field);
        if (index > 0 && parent.Controls[index - 1] is Label label) label.Visible = visible;
    }

    private sealed class PreviewOnlyTransport : ISocialTransport
    {
        public Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken token) => throw new InvalidOperationException("لا يُسمح بالإرسال في المعاينة.");
        public Task<string> CheckConnectionAsync(CancellationToken token) => throw new InvalidOperationException("المعاينة محلية.");
    }
}
