using System.ComponentModel;
using System.Text;
using System.Text.Json;
using SenderPro.Core;

namespace SenderPro.Windows;

public sealed partial class MainForm : Form
{
    private readonly DataStore store;
    private readonly AppData data;
    private readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(28), AutoScroll = true };
    private readonly FlowLayoutPanel navigation = new() { Dock = DockStyle.Right, Width = 230,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16), BackColor = Theme.Sidebar, AutoScroll = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 35, Padding = new Padding(12, 6, 12, 0) };
    private readonly List<ModernButton> navigationButtons = [];
    private CancellationTokenSource? cancellation;
    private string smtpPassword = "";

    public MainForm(DataStore store, AppData data)
    {
        this.store = store; this.data = data;
        Text = "Sender Pro | إدارة التواصل";
        Size = new Size(1250, 820); MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = false;
        Font = new Font("Segoe UI", 10); BackColor = Theme.Background; ForeColor = Theme.Ink;
        Controls.Add(content); Controls.Add(navigation); Controls.Add(status);
        var brand = new Label { Text = "SENDER PRO\nمساحة عملك للتواصل", ForeColor = Color.White,
            Width = 192, Height = 100, Font = new Font("Segoe UI", 13, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        navigation.Controls.Add(brand);
        AddNav("لوحة التحكم", Dashboard);
        AddNav("جهات الاتصال", Contacts);
        AddNav("قوالب الرسائل", Templates);
        AddNav("مكتبة المحتوى", ContentLibrary);
        AddNav("حملة جديدة", Campaign);
        AddNav("سجل الحملات", Reports);
        AddNav("إعداد البريد", Settings);
        AddNav("ربط واتساب وتليجرام", MessagingAccounts);
        AddNav("خريطة الوظائف", Catalog);
        navigation.Controls.Add(new Label { Text = "WINDOWS EDITION  •  0.3", ForeColor = Theme.Muted, Width = 192, Height = 50, TextAlign = ContentAlignment.MiddleCenter });
        status.ForeColor = Theme.Muted; status.BackColor = Color.White; status.AutoEllipsis = true;
        status.Text = "جاهز  •  حفظ محلي تلقائي  •  كلمات المرور لا تُحفظ على القرص";
        FormClosing += (_, e) =>
        {
            if (cancellation is not null)
            {
                cancellation.Cancel(); e.Cancel = true;
                status.Text = "جارٍ إيقاف الحملة وحفظ النتائج؛ أغلق النافذة بعد التوقف.";
            }
            else { try { Save(); } catch { e.Cancel = true; } }
        };
        Dashboard();
    }

    private void AddNav(string text, Action action)
    {
        var button = (ModernButton)Button(text, (_, _) => { });
        button.Width = 192; button.Height = 48; button.AutoSize = false;
        button.BackColor = Theme.Sidebar; button.ForeColor = Color.FromArgb(196, 208, 225);
        button.Margin = new Padding(0, 0, 0, 6);
        button.Click += (_, _) => Guard(() =>
        {
            foreach (var item in navigationButtons) { item.Selected = item == button; item.Invalidate(); }
            action();
        });
        navigationButtons.Add(button); navigation.Controls.Add(button);
    }

    private static Button Button(string text, EventHandler action)
    {
        var button = new ModernButton { Text = text, AutoSize = false, Width = Math.Max(125, TextRenderer.MeasureText(text, SystemFonts.MessageBoxFont).Width + 38),
            MinimumSize = new Size(100, 42), Margin = new Padding(4) };
        if (text.Contains("إنشاء") || text.Contains("إرسال عبر") || text.StartsWith("حفظ"))
        { button.BackColor = Theme.Accent; button.ForeColor = Color.White; }
        button.Click += action; return button;
    }

    private FlowLayoutPanel Page(string title, string description)
    {
        content.SuspendLayout();
        while (content.Controls.Count > 0) { var old = content.Controls[0]; content.Controls.Remove(old); old.Dispose(); }
        content.AutoScrollPosition = Point.Empty;
        var page = new ResponsivePage();
        page.Controls.Add(new Label { Text = "مساحة العمل  /  " + title, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 10) });
        page.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font(Font.FontFamily, 24, FontStyle.Bold), ForeColor = Theme.Ink, Margin = new Padding(0, 0, 0, 10) });
        page.Controls.Add(new Label { Text = description, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 24) });
        content.Controls.Add(page); content.ResumeLayout(true); page.Fit(); return page;
    }

    private static FlowLayoutPanel Toolbar() => new() { AutoSize = true, WrapContents = true,
        FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 12) };

    private static DataGridView Grid()
    {
        var grid = new DataGridView { Width = 850, Height = 400, AutoGenerateColumns = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 44, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, GridColor = Theme.Line,
            Margin = new Padding(0, 0, 0, 16) };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(233, 239, 247), ForeColor = Theme.Ink,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(8) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Theme.Ink,
            SelectionBackColor = Color.FromArgb(219, 243, 239), SelectionForeColor = Theme.Ink, Padding = new Padding(6) };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        grid.RowTemplate.Height = 40;
        grid.CellFormatting += (_, e) =>
        {
            if (grid.Columns[e.ColumnIndex].DataPropertyName is "Status" or "State" or "Mode" or "Channel" && e.Value is string code)
            { e.Value = FriendlyState(code); e.FormattingApplied = true; }
        };
        return grid;
    }

    private static string FriendlyState(string value) => value switch
    {
        "accepted" => "قبله المزود", "held" => "معلق لدى واتساب", "unknown" => "النتيجة غير مؤكدة", "failed" => "فشل", "skipped" => "مستبعد", "preview" => "معاينة",
        "created" => "جاهزة", "running" => "قيد التنفيذ", "completed" => "مكتملة", "cancelled" => "متوقفة",
        "interrupted" => "انقطع التنفيذ", "paused" => "متوقفة — راجع الحساب أو حد المزود", "smtp" => "بريد SMTP", "telegram" => "تليجرام", "whatsapp" => "واتساب", _ => value
    };

    private static void Column(DataGridView grid, string property, string title, bool check = false)
    {
        DataGridViewColumn column = check ? new DataGridViewCheckBoxColumn() : new DataGridViewTextBoxColumn();
        column.DataPropertyName = property; column.HeaderText = title; grid.Columns.Add(column);
    }

    private void Save() { store.Save(data); }
    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "تعذر إتمام العملية", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Dashboard()
    {
        navigationButtons[0].Selected = true; navigationButtons[0].Invalidate();
        var page = Page("نظرة عامة", "جهات اتصالك، رسائلك ونتائج حملاتك في مساحة عمل واحدة.");
        var cards = new TableLayoutPanel { Height = 230, Width = 850, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 0, 0, 20) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var stats = new[] { ("جهات الاتصال", data.Contacts.Count.ToString(), "أشخاص ومجموعات"),
            ("قوالب الرسائل", data.Templates.Count.ToString(), "محتوى قابل لإعادة الاستخدام"),
            ("الحملات", data.Campaigns.Count.ToString(), "سجل محفوظ محليًا"),
            ("رسائل قبلها الخادم", data.Campaigns.Sum(c => c.Results.Count(r => r.Status == "accepted")).ToString(), "قبول SMTP لا يؤكد وصول الرسالة") };
        for (var i = 0; i < stats.Length; i++)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), Margin = new Padding(5) };
            card.Controls.Add(new Label { Text = stats[i].Item3, Dock = DockStyle.Bottom, Height = 22, ForeColor = Theme.Muted });
            card.Controls.Add(new Label { Text = stats[i].Item2, Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 26, FontStyle.Bold), ForeColor = Theme.Accent });
            card.Controls.Add(new Label { Text = stats[i].Item1, Dock = DockStyle.Top, Height = 24, ForeColor = Theme.Ink });
            cards.Controls.Add(card, i % 2, i / 2);
        }
        page.Controls.Add(cards);
        var actions = Toolbar(); actions.Controls.Add(Button("إنشاء حملة جديدة", (_, _) => Campaign()));
        actions.Controls.Add(Button("إدارة جهات الاتصال", (_, _) => Contacts())); page.Controls.Add(actions);
        page.Controls.Add(new Label { Text = "آخر الحملات", AutoSize = true, Font = new Font(Font.FontFamily, 14, FontStyle.Bold), Margin = new Padding(0, 10, 0, 12) });
        if (data.Campaigns.Count == 0)
        {
            page.Controls.Add(new Label { Text = "لم تبدأ أي حملة بعد. أضف جهات الاتصال ثم جرّب معاينة رسالتك قبل الإرسال.",
                AutoSize = true, BackColor = Color.White, Padding = new Padding(24), ForeColor = Theme.Muted });
        }
        else
        {
            var recent = Grid(); recent.Height = 220; recent.ReadOnly = true;
            Column(recent, "Name", "الحملة"); Column(recent, "State", "الحالة"); Column(recent, "CreatedAt", "التاريخ");
            recent.DataSource = data.Campaigns.OrderByDescending(c => c.CreatedAt).Take(5).ToList(); page.Controls.Add(recent);
        }
    }

    private void Contacts()
    {
        var page = Page("جهات الاتصال", "استورد قائمة CSV أو Excel، صنّفها إلى مجموعات وعدّل بياناتها مباشرة. الحفظ تلقائي بعد تعديل الخلايا.");
        var grid = Grid();
        Column(grid, "Name", "الاسم"); Column(grid, "Email", "البريد"); Column(grid, "Phone", "الهاتف");
        Column(grid, "TelegramChatId", "معرّف تليجرام"); Column(grid, "Group", "المجموعة"); Column(grid, "Unsubscribed", "إلغاء الاشتراك", true);
        var search = new TextBox { Width = 270, PlaceholderText = "ابحث بالاسم أو البريد أو الهاتف" };
        void Bind() => grid.DataSource = new BindingList<Contact>(data.Contacts.Where(c =>
            (c.Name + " " + c.Email + " " + c.Phone + " " + c.TelegramChatId).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList());
        void Commit() { grid.EndEdit(); if (BindingContext?[grid.DataSource!] is CurrencyManager manager) manager.EndCurrentEdit(); Save(); }
        grid.DataError += (_, e) => { e.ThrowException = false; MessageBox.Show("قيمة غير صالحة في الجدول."); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellEndEdit += (_, _) => Guard(Save);
        search.TextChanged += (_, _) => Bind();
        var bar = Toolbar(); bar.Controls.Add(search);
        bar.Controls.Add(Button("إضافة", (_, _) => Guard(() =>
        {
            using var dialog = new Editor("جهة اتصال جديدة", ("الاسم", ""), ("البريد", ""), ("الهاتف الدولي", ""), ("المجموعة", ""), ("معرّف محادثة تليجرام", ""));
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dialog.Value(0))) throw new InvalidDataException("أدخل اسم جهة الاتصال.");
            var contact = new Contact { Name = dialog.Value(0), Email = dialog.Value(1).Trim(), Phone = dialog.Value(2).Trim(), Group = dialog.Value(3), TelegramChatId = dialog.Value(4).Trim() };
            if (ContactCsv.Merge(data.Contacts, [contact]) == 0) MessageBox.Show("جهة الاتصال موجودة بالفعل؛ لم تُضف نسخة مكررة.");
            Save(); Bind();
        })));
        bar.Controls.Add(Button("حذف المحدد", (_, _) => Guard(() =>
        {
            if (grid.CurrentRow?.DataBoundItem is Contact c) { data.Contacts.Remove(c); Save(); Bind(); }
        })));
        bar.Controls.Add(Button("استيراد قائمة", (_, _) => Guard(() =>
        {
            using var file = new OpenFileDialog { Filter = "قوائم جهات الاتصال|*.csv;*.xlsx|CSV|*.csv|Excel|*.xlsx" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            var imported = Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? ContactWorkbook.Read(file.FileName) : ContactCsv.Read(File.ReadAllText(file.FileName, Encoding.UTF8));
            var count = ContactCsv.Merge(data.Contacts, imported); Save(); Bind();
            MessageBox.Show($"أُضيفت {count} جهة اتصال. تم دمج المكرر والحفاظ على إلغاء الاشتراك.");
        })));
        bar.Controls.Add(Button("تصدير CSV", (_, _) => Guard(() => { Commit(); Export(ContactCsv.Write(data.Contacts), "contacts.csv"); })));
        bar.Controls.Add(Button("تصدير Excel", (_, _) => Guard(() =>
        {
            Commit(); using var file = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "contacts.xlsx" };
            if (file.ShowDialog(this) == DialogResult.OK) ContactWorkbook.Write(file.FileName, data.Contacts);
        })));
        bar.Controls.Add(Button("حفظ", (_, _) => Guard(Commit)));
        page.Controls.Add(bar); page.Controls.Add(grid); Bind();
    }

    private void Templates()
    {
        var page = Page("قوالب الرسائل", "المتغيرات المتاحة: {name}، {email}، {phone}، {group}، {telegram_chat_id}. تُستبدل ببيانات كل مستلم.");
        var list = new ListBox { Width = 850, Height = 220, DisplayMember = "Name" };
        void Bind() { list.DataSource = null; list.DataSource = data.Templates.ToList(); }
        void Edit(bool add)
        {
            var selected = add ? new MessageTemplate() : list.SelectedItem as MessageTemplate;
            if (selected is null) return;
            using var dialog = new Editor("قالب الرسالة", ("اسم القالب", selected.Name), ("عنوان البريد", selected.Subject), ("نص الرسالة", selected.Body));
            dialog.Multiline(2);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            selected.Name = dialog.Value(0); selected.Subject = dialog.Value(1); selected.Body = dialog.Value(2);
            if (add) data.Templates.Add(selected);
            Save(); Bind();
        }
        var bar = Toolbar();
        bar.Controls.Add(Button("إضافة قالب", (_, _) => Guard(() => Edit(true))));
        bar.Controls.Add(Button("تعديل", (_, _) => Guard(() => Edit(false))));
        bar.Controls.Add(Button("حذف", (_, _) => Guard(() =>
        { if (list.SelectedItem is MessageTemplate t) { data.Templates.Remove(t); Save(); Bind(); } })));
        page.Controls.Add(bar); page.Controls.Add(list); Bind();
    }

    private void Reports()
    {
        var page = Page("سجل الحملات", "accepted يعني أن خادم SMTP قبل الرسالة؛ لا يؤكد وصولها. preview يعني معاينة دون إرسال. skipped يعني استبعاد المستلم. failed يعني فشل العملية.");
        var list = new ListBox { Width = 850, Height = 170 };
        var records = data.Campaigns.OrderByDescending(c => c.CreatedAt).ToList();
        list.Items.AddRange(records.Select(c => (object)$"{c.CreatedAt.ToLocalTime():g} | {c.Name} | {FriendlyState(c.Channel)} | {FriendlyState(c.Mode)} | {FriendlyState(c.State)}").ToArray());
        var grid = Grid(); grid.Height = 320; grid.ReadOnly = true;
        Column(grid, "Recipient", "المستلم"); Column(grid, "Status", "الحالة"); Column(grid, "Detail", "التفاصيل"); Column(grid, "ProviderMessageId", "معرّف المزود"); Column(grid, "At", "التوقيت");
        list.SelectedIndexChanged += (_, _) => grid.DataSource = list.SelectedIndex < 0 ? null : records[list.SelectedIndex].Results;
        page.Controls.Add(list); page.Controls.Add(grid);
        page.Controls.Add(Button("تصدير نتائج الحملة", (_, _) => Guard(() =>
        {
            if (list.SelectedIndex < 0) return;
            static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var text = "recipient,status,detail,provider_message_id,at\r\n" + string.Join("\r\n", records[list.SelectedIndex].Results.Select(r =>
                string.Join(",", new[] { r.Recipient, r.Status, r.Detail, r.ProviderMessageId, r.At.ToString("O") }.Select(Q))));
            Export(text, "campaign-results.csv");
        })));
        if (list.Items.Count > 0) list.SelectedIndex = 0;
    }

    private void Settings()
    {
        var page = Page("إعدادات SMTP", "يدعم STARTTLS، عادةً على المنفذ 587. كلمة المرور تبقى في الذاكرة فقط وتُدخل مجددًا بعد إغلاق البرنامج. منفذ 465 الذي يحتاج TLS ضمنيًا غير مدعوم بهذا النقل.");
        var host = Field(page, "خادم SMTP", data.Smtp.Host);
        var port = Field(page, "المنفذ", data.Smtp.Port.ToString());
        var from = Field(page, "عنوان المرسل", data.Smtp.FromAddress);
        var user = Field(page, "اسم المستخدم", data.Smtp.Username);
        var password = Field(page, "كلمة المرور", smtpPassword); password.UseSystemPasswordChar = true;
        var delay = Field(page, "التأخير بين الرسائل بالمللي ثانية", data.Smtp.DelayMilliseconds.ToString());
        var tls = new CheckBox { Text = "استخدام STARTTLS", Checked = data.Smtp.EnableTls, AutoSize = true }; page.Controls.Add(tls);
        page.Controls.Add(Button("حفظ الإعدادات", (_, _) => Guard(() =>
        {
            if (!int.TryParse(port.Text, out var p) || p < 1 || p > 65535) throw new InvalidDataException("أدخل منفذًا صالحًا.");
            if (!int.TryParse(delay.Text, out var d) || d < 0 || d > 3600000) throw new InvalidDataException("التأخير من 0 إلى 3600000 مللي ثانية.");
            var next = new SmtpSettings { Host = host.Text.Trim(), Port = p, FromAddress = from.Text.Trim(), Username = user.Text.Trim(), EnableTls = tls.Checked, DelayMilliseconds = d };
            if (string.IsNullOrWhiteSpace(next.Host) || !CampaignRunner.IsEmail(next.FromAddress)) throw new InvalidDataException("أدخل اسم الخادم وعنوان المرسل الصحيح.");
            data.Smtp = next; smtpPassword = password.Text; Save(); status.Text = "تم حفظ إعدادات الاتصال  •  كلمة المرور لهذه الجلسة فقط";
        })));
    }

    private void ValidateSmtp()
    {
        if (string.IsNullOrWhiteSpace(data.Smtp.Host) || !CampaignRunner.IsEmail(data.Smtp.FromAddress))
            throw new InvalidDataException("احفظ إعدادات SMTP أولًا.");
        if (!string.IsNullOrWhiteSpace(data.Smtp.Username) && string.IsNullOrEmpty(smtpPassword))
            throw new InvalidDataException("أدخل كلمة مرور SMTP في الإعدادات لهذه الجلسة.");
    }

    private void Catalog()
    {
        var page = Page("قنوات التواصل", "البريد وتليجرام وواتساب مدمجة. الإرسال يتطلب حسابًا وإعدادات صالحة لكل قناة؛ بقية التكاملات موضحة بحالتها.");
        var features = JsonSerializer.Deserialize<List<Feature>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "feature-catalog.json"))) ?? [];
        var channels = new[] { ("البريد الإلكتروني", "Emails", "متاح — SMTP والمرفقات"),
            ("WhatsApp", "Whatsapp", "Cloud API مدمج — يحتاج حساب Business"), ("Facebook", "FacebookTools", "التكامل غير منفذ"),
            ("Instagram", "InstagramTools", "التكامل غير منفذ"), ("Telegram", "Telegram", "Bot API مدمج — يحتاج رمز بوت"),
            ("X / Twitter", "TwitterTools", "التكامل غير منفذ"), ("LinkedIn", "LinkedinTools", "التكامل غير منفذ"),
            ("TikTok", "TikTok", "التكامل غير منفذ"), ("YouTube", "YoutubeTools", "التكامل غير منفذ"),
            ("Pinterest", "Pinterest", "التكامل غير منفذ"), ("VK", "VkTools", "التكامل غير منفذ"),
            ("SMS", "SMS", "التكامل غير منفذ") };
        var rows = channels.Select(c => new { Channel = c.Item1, Status = c.Item3,
            ReferenceTools = features.Count(f => f.Module == c.Item2 || (c.Item2 == "Telegram" && f.Source.Contains("Telegram"))) }).ToList();
        var grid = Grid(); grid.Height = 540; grid.ReadOnly = true;
        Column(grid, "Channel", "القناة"); Column(grid, "Status", "التوفر"); Column(grid, "ReferenceTools", "أدوات مرجعية تحتاج مراجعة");
        grid.DataSource = rows; page.Controls.Add(grid);
        page.Controls.Add(Button("إعداد اتصال البريد", (_, _) => Settings()));
    }

    private static TextBox Field(Control parent, string title, string value)
    {
        parent.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 8, 0, 3) });
        var field = new TextBox { Text = value, Width = 650, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) }; parent.Controls.Add(field); return field;
    }

    private void Export(string text, string name)
    {
        using var file = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = name };
        if (file.ShowDialog(this) == DialogResult.OK) File.WriteAllText(file.FileName, text, new UTF8Encoding(true));
    }

    internal void RunUiSmoke(string output)
    {
        uiSmokeRunning = true; Show();
        void OpenChannel(int index, bool serviceWindow = false)
        {
            Campaign(); smokeChannel!.SelectedIndex = index;
            if (serviceWindow && content.Controls.Find("whatsapp-mode", true).FirstOrDefault() is ComboBox mode) mode.SelectedIndex = 1;
        }
        var pages = new (string Name, Action Open)[] { ("dashboard", Dashboard), ("contacts", Contacts),
            ("templates", Templates), ("library", ContentLibrary), ("campaign-smtp", () => OpenChannel(0)),
            ("campaign-telegram", () => OpenChannel(1)), ("campaign-whatsapp-template", () => OpenChannel(2)),
            ("campaign-whatsapp-text", () => OpenChannel(2, true)), ("reports", Reports),
            ("settings-smtp", Settings), ("settings-messaging", MessagingAccounts), ("catalog", Catalog) };
        var report = new List<string>();
        void Capture(string name)
        {
            using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
        void FinishUiTask(Task task)
        {
            var deadline = Environment.TickCount64 + 30000;
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                if (Environment.TickCount64 > deadline) throw new TimeoutException("Local preview timed out.");
            }
            task.GetAwaiter().GetResult();
        }
        void CheckBounds(Control parent, string route)
        {
            if (parent is DataGridView or TextBoxBase or ComboBox or ListBox) return;
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                if (child.Left < -2 || child.Right > parent.ClientSize.Width + 2)
                    throw new InvalidOperationException($"Horizontal clipping in {route}: {child.GetType().Name} {child.Bounds} within {parent.GetType().Name} {parent.ClientSize}");
                CheckBounds(child, route);
            }
        }
        foreach (var size in new[] { new Size(900, 640), new Size(1280, 820), new Size(1600, 1000) })
        {
            Size = size;
            foreach (var (name, open) in pages)
            {
                open(); Application.DoEvents(); PerformLayout(); Application.DoEvents();
                if (content.Controls[0] is not ResponsivePage page) throw new InvalidOperationException("Missing page " + name);
                page.Fit(); page.PerformLayout(); Application.DoEvents(); CheckBounds(page, name);
                Capture($"{name}-{size.Width}");
                if (name.StartsWith("campaign-"))
                {
                    FinishUiTask(smokePreview!()); Application.DoEvents();
                    var record = data.Campaigns.Last();
                    if (record.State != "completed" || record.Results.Count != data.Contacts.Count || record.Results.Any(r => r.Status != "preview"))
                        throw new InvalidOperationException("Preview did not execute expected local recipients on " + name);
                    var resultGrid = content.Controls.Find("campaign-results", true).Single();
                    content.ScrollControlIntoView(resultGrid); Application.DoEvents(); Capture($"{name}-results-{size.Width}");
                    report.Add($"PASS local preview {name}: {record.Results.Count} recipients; no live transport");
                }
                report.Add($"PASS {name} at {size.Width}x{size.Height}");
            }
            using var editor = new ContentLibraryEditor(data.ContentItems.First());
            editor.Width = size.Width == 900 ? 560 : size.Width == 1280 ? 740 : 820;
            editor.Show(this); Application.DoEvents(); CheckBounds(editor, "content-editor");
            using var editorBitmap = new Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(editorBitmap, new Rectangle(0, 0, editor.Width, editor.Height));
            editorBitmap.Save(Path.Combine(output, $"library-editor-{size.Width}.png"));
            report.Add($"PASS content editor at {editor.Width} width"); editor.Close();
        }
        var originalContacts = data.Contacts;
        try
        {
            data.Contacts = Enumerable.Range(1, 5000).Select(i => new Contact { Name = "بيانات اختبار " + i, Email = $"sample{i}@example.com" }).ToList();
            Campaign(); var previewTask = smokePreview!();
            if (previewTask.IsCompleted) throw new InvalidOperationException("Large preview blocked the UI until completion.");
            ((Button)content.Controls.Find("campaign-stop", true).Single()).PerformClick(); FinishUiTask(previewTask);
            if (data.Campaigns.Last().State != "cancelled" || data.Campaigns.Last().Results.Count >= 5000)
                throw new InvalidOperationException("Large preview cancellation did not stop pending contacts.");
            report.Add("PASS 5000-recipient preview yields to UI and cancellation stops pending contacts");
        }
        finally { data.Contacts = originalContacts; }
        File.WriteAllLines(Path.Combine(output, "ui-checks.txt"), report); Close();
    }

    private sealed class Feature
    {
        public string Module { get; set; } = "";
        public string Source { get; set; } = "";
        public string Status { get; set; } = "غير منفذ — يحتاج تحديد السلوك";
    }

    private sealed class Editor : Form
    {
        private readonly List<TextBox> fields = [];
        public Editor(string title, params (string Label, string Value)[] values)
        {
            Text = title; Size = new Size(740, 580); StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10); RightToLeft = RightToLeft.Yes; RightToLeftLayout = false;
            var panel = new ResponsivePage { Dock = DockStyle.Fill, AutoSize = false, AutoScroll = true, Padding = new Padding(20) };
            Controls.Add(panel);
            foreach (var (label, value) in values) fields.Add(Field(panel, label, value));
            var save = Button("حفظ", (_, _) => { DialogResult = DialogResult.OK; Close(); });
            var cancel = Button("إلغاء", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
            panel.Controls.Add(save); panel.Controls.Add(cancel); CancelButton = cancel;
        }
        public string Value(int i) => fields[i].Text;
        public void Multiline(int i) { fields[i].Multiline = true; fields[i].Height = 170; fields[i].ScrollBars = ScrollBars.Vertical; }
    }
}
