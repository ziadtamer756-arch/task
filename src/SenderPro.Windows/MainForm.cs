using System.ComponentModel;
using System.Text;
using System.Text.Json;
using SenderPro.Core;

namespace SenderPro.Windows;

public sealed class MainForm : Form
{
    private readonly DataStore store;
    private readonly AppData data;
    private readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(24), AutoScroll = true };
    private readonly FlowLayoutPanel navigation = new() { Dock = DockStyle.Right, Width = 220,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), BackColor = Color.FromArgb(24, 34, 54) };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 35, Padding = new Padding(12, 6, 12, 0) };
    private CancellationTokenSource? cancellation;
    private string smtpPassword = "";

    public MainForm(DataStore store, AppData data)
    {
        this.store = store; this.data = data;
        Text = "Sender Pro — النسخة الجديدة";
        Size = new Size(1250, 820); MinimumSize = new Size(950, 680);
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251);
        Controls.Add(content); Controls.Add(navigation); Controls.Add(status);
        var brand = new Label { Text = "SENDER PRO\nإدارة التواصل والحملات", ForeColor = Color.White,
            Width = 192, Height = 85, Font = new Font("Segoe UI", 13, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        navigation.Controls.Add(brand);
        AddNav("لوحة التحكم", Dashboard);
        AddNav("جهات الاتصال", Contacts);
        AddNav("قوالب الرسائل", Templates);
        AddNav("حملة بريد جديدة", Campaign);
        AddNav("سجل الحملات", Reports);
        AddNav("إعدادات SMTP", Settings);
        AddNav("أقسام البرنامج الأصلي", Catalog);
        status.Text = "البيانات محفوظة محليًا — " + store.FilePath;
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
        var button = Button(text, (_, _) => Guard(action));
        button.Width = 192; button.Height = 48; button.BackColor = Color.FromArgb(38, 52, 78);
        button.ForeColor = Color.White; navigation.Controls.Add(button);
    }

    private static Button Button(string text, EventHandler action)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(125, 38),
            Padding = new Padding(8), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(4) };
        button.Click += action; return button;
    }

    private FlowLayoutPanel Page(string title, string description)
    {
        content.Controls.Clear();
        var page = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, RightToLeft = RightToLeft.Yes };
        page.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font(Font.FontFamily, 22, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) });
        page.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(820, 0), Margin = new Padding(0, 0, 0, 16) });
        content.Controls.Add(page); return page;
    }

    private static FlowLayoutPanel Toolbar() => new() { AutoSize = true, WrapContents = true,
        FlowDirection = FlowDirection.RightToLeft, MaximumSize = new Size(850, 0) };

    private static DataGridView Grid() => new() { Width = 850, Height = 440, AutoGenerateColumns = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };

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
        var page = Page("لوحة التحكم", "نسخة Windows جديدة. الأقسام المنفذة حاليًا: جهات الاتصال، القوالب، حملات البريد، التقارير وإعدادات SMTP.");
        foreach (var text in new[] { $"جهات الاتصال: {data.Contacts.Count}",
            $"القوالب: {data.Templates.Count}", $"الحملات: {data.Campaigns.Count}",
            $"رسائل قبلها خادم SMTP: {data.Campaigns.Sum(c => c.Results.Count(r => r.Status == "accepted"))}" })
            page.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(14),
                BackColor = Color.White, Margin = new Padding(0, 0, 0, 10), Font = new Font(Font.FontFamily, 14) });
        page.Controls.Add(Button("إنشاء حملة بريد", (_, _) => Campaign()));
        page.Controls.Add(new Label { Text = "تكاملات المنصات الاجتماعية وتصميم الشاشات المطابق للأصل لم تُنفذ بعد. راجع حصر الأقسام.",
            AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(0, 20, 0, 0) });
    }

    private void Contacts()
    {
        var page = Page("جهات الاتصال", "استورد CSV بالأعمدة name,email,phone,group,unsubscribed. القيم المقبولة لإلغاء الاشتراك: true/false أو 1/0. يمكن تعديل الخلايا ثم حفظها.");
        var grid = Grid();
        Column(grid, "Name", "الاسم"); Column(grid, "Email", "البريد"); Column(grid, "Phone", "الهاتف");
        Column(grid, "Group", "المجموعة"); Column(grid, "Unsubscribed", "إلغاء الاشتراك", true);
        var search = new TextBox { Width = 270, PlaceholderText = "ابحث بالاسم أو البريد أو الهاتف" };
        void Bind() => grid.DataSource = new BindingList<Contact>(data.Contacts.Where(c =>
            (c.Name + " " + c.Email + " " + c.Phone).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList());
        void Commit() { grid.EndEdit(); if (BindingContext?[grid.DataSource!] is CurrencyManager manager) manager.EndCurrentEdit(); Save(); }
        grid.DataError += (_, e) => { e.ThrowException = false; MessageBox.Show("قيمة غير صالحة في الجدول."); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellEndEdit += (_, _) => Guard(Save);
        search.TextChanged += (_, _) => Bind();
        var bar = Toolbar(); bar.Controls.Add(search);
        bar.Controls.Add(Button("إضافة", (_, _) => Guard(() =>
        {
            using var dialog = new Editor("جهة اتصال جديدة", ("الاسم", ""), ("البريد", ""), ("الهاتف", ""), ("المجموعة", ""));
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dialog.Value(0))) throw new InvalidDataException("أدخل اسم جهة الاتصال.");
            data.Contacts.Add(new Contact { Name = dialog.Value(0), Email = dialog.Value(1), Phone = dialog.Value(2), Group = dialog.Value(3) });
            Save(); Bind();
        })));
        bar.Controls.Add(Button("حذف المحدد", (_, _) => Guard(() =>
        {
            if (grid.CurrentRow?.DataBoundItem is Contact c) { data.Contacts.Remove(c); Save(); Bind(); }
        })));
        bar.Controls.Add(Button("استيراد CSV", (_, _) => Guard(() =>
        {
            using var file = new OpenFileDialog { Filter = "CSV files|*.csv" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            var imported = ContactCsv.Read(File.ReadAllText(file.FileName, Encoding.UTF8));
            var count = ContactCsv.Merge(data.Contacts, imported); Save(); Bind();
            MessageBox.Show($"أُضيفت {count} جهة اتصال. تم دمج المكرر والحفاظ على إلغاء الاشتراك.");
        })));
        bar.Controls.Add(Button("تصدير CSV", (_, _) => Guard(() => { Commit(); Export(ContactCsv.Write(data.Contacts), "contacts.csv"); })));
        bar.Controls.Add(Button("حفظ", (_, _) => Guard(Commit)));
        page.Controls.Add(bar); page.Controls.Add(grid); Bind();
    }

    private void Templates()
    {
        var page = Page("قوالب الرسائل", "المتغيرات المتاحة: {name}، {email}، {phone}، {group}. تُستبدل ببيانات كل مستلم.");
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

    private void Campaign()
    {
        var page = Page("حملة بريد جديدة", "اختر مجموعة أو جميع جهات الاتصال. المعاينة محلية ولا ترسل رسائل. الإرسال الفعلي يحتاج إعدادات SMTP. يتم استبعاد العناوين المكررة وغير الصالحة وجهات الاتصال الملغية للاشتراك.");
        var name = Field(page, "اسم الحملة", "حملة " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        var group = new ComboBox { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList };
        group.Items.Add("كل المجموعات"); group.Items.AddRange(data.Contacts.Select(c => c.Group).Where(g => g.Length > 0).Distinct().Cast<object>().ToArray()); group.SelectedIndex = 0;
        page.Controls.Add(group);
        var template = new ComboBox { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
        template.Items.AddRange(data.Templates.Cast<object>().ToArray()); page.Controls.Add(template);
        var subject = Field(page, "عنوان البريد", "مرحبًا {name}");
        var body = Field(page, "نص الرسالة", "مرحبًا {name}،\r\n"); body.Multiline = true; body.Height = 130; body.ScrollBars = ScrollBars.Vertical;
        template.SelectedIndexChanged += (_, _) => { if (template.SelectedItem is MessageTemplate t) { subject.Text = t.Subject; body.Text = t.Body; } };
        if (template.Items.Count > 0) template.SelectedIndex = 0;
        var progress = new Label { AutoSize = true, Text = "جاهز" };
        var results = Grid(); results.Height = 230; results.ReadOnly = true;
        Column(results, "Recipient", "المستلم"); Column(results, "Status", "الحالة"); Column(results, "Detail", "التفاصيل");
        var displayed = new BindingList<DeliveryResult>(); results.DataSource = displayed;
        var bar = Toolbar();
        var preview = Button("معاينة الحملة", (_, _) => { });
        var send = Button("إرسال عبر SMTP", (_, _) => { });
        var stop = Button("إيقاف", (_, _) => cancellation?.Cancel()); stop.Enabled = false;
        preview.Click += async (_, _) => await Run(true);
        send.Click += async (_, _) => await Run(false);
        bar.Controls.Add(preview); bar.Controls.Add(send); bar.Controls.Add(stop);
        page.Controls.Add(bar); page.Controls.Add(progress); page.Controls.Add(results);

        async Task Run(bool previewOnly)
        {
            if (cancellation is not null) return;
            CampaignRecord? record = null;
            try
            {
                if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(subject.Text) || string.IsNullOrWhiteSpace(body.Text))
                    throw new InvalidDataException("أدخل اسم الحملة والعنوان ونص الرسالة.");
                var selected = data.Contacts.Where(c => group.SelectedIndex == 0 || c.Group == (string?)group.SelectedItem)
                    .Select(c => new Contact { Name = c.Name, Email = c.Email, Phone = c.Phone, Group = c.Group, Unsubscribed = c.Unsubscribed }).ToList();
                if (selected.Count == 0) throw new InvalidDataException("لا توجد جهات اتصال في المجموعة المحددة.");
                if (!previewOnly)
                {
                    ValidateSmtp();
                    if (MessageBox.Show($"إرسال الحملة عبر SMTP إلى المجموعة المختارة ({selected.Count} سجلًا قبل التصفية)؟",
                        "إرسال الحملة", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                }
                record = new CampaignRecord { Name = name.Text, Subject = subject.Text, Body = body.Text,
                    Mode = previewOnly ? "preview" : "smtp" };
                data.Campaigns.Add(record); Save(); displayed.Clear();
                cancellation = new CancellationTokenSource();
                preview.Enabled = send.Enabled = navigation.Enabled = name.Enabled = group.Enabled = template.Enabled = subject.Enabled = body.Enabled = false;
                stop.Enabled = true;
                var runner = new CampaignRunner(new SmtpTransport(data.Smtp, smtpPassword));
                await runner.RunAsync(record, selected, previewOnly, data.Smtp.DelayMilliseconds, r =>
                {
                    displayed.Add(r); Save(); progress.Text = $"تمت معالجة {record.Results.Count} من {selected.Count}";
                }, cancellation.Token);
                progress.Text = $"حالة الحملة: {record.State} — تمت معالجة {record.Results.Count} سجلًا";
            }
            catch (Exception ex) { if (record is not null) record.State = "failed"; MessageBox.Show(ex.Message, "تعذر تشغيل الحملة"); }
            finally
            {
                cancellation?.Dispose(); cancellation = null;
                preview.Enabled = send.Enabled = navigation.Enabled = name.Enabled = group.Enabled = template.Enabled = subject.Enabled = body.Enabled = true;
                stop.Enabled = false; Guard(Save);
            }
        }
    }

    private void Reports()
    {
        var page = Page("سجل الحملات", "accepted يعني أن خادم SMTP قبل الرسالة؛ لا يؤكد وصولها. preview يعني معاينة دون إرسال. skipped يعني استبعاد المستلم. failed يعني فشل العملية.");
        var list = new ListBox { Width = 850, Height = 170 };
        var records = data.Campaigns.OrderByDescending(c => c.CreatedAt).ToList();
        list.Items.AddRange(records.Select(c => (object)$"{c.CreatedAt.ToLocalTime():g} | {c.Name} | {c.Mode} | {c.State}").ToArray());
        var grid = Grid(); grid.Height = 320; grid.ReadOnly = true;
        Column(grid, "Recipient", "المستلم"); Column(grid, "Status", "الحالة"); Column(grid, "Detail", "التفاصيل"); Column(grid, "At", "التوقيت");
        list.SelectedIndexChanged += (_, _) => grid.DataSource = list.SelectedIndex < 0 ? null : records[list.SelectedIndex].Results;
        page.Controls.Add(list); page.Controls.Add(grid);
        page.Controls.Add(Button("تصدير نتائج الحملة", (_, _) => Guard(() =>
        {
            if (list.SelectedIndex < 0) return;
            static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var text = "recipient,status,detail,at\r\n" + string.Join("\r\n", records[list.SelectedIndex].Results.Select(r =>
                string.Join(",", new[] { r.Recipient, r.Status, r.Detail, r.At.ToString("O") }.Select(Q))));
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
            data.Smtp = next; smtpPassword = password.Text; Save(); status.Text = "تم حفظ إعدادات SMTP. كلمة المرور لم تُحفظ على القرص.";
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
        var page = Page("حصر أقسام البرنامج الأصلي", "هذه أسماء الملفات المستخرجة من معلومات التصحيح، وليست وظائف منفذة في النسخة الجديدة. العدد لا يشمل ملفات Designer. نحتاج فحص الشاشات والسلوك لتحديد تفاصيل كل قسم.");
        var features = JsonSerializer.Deserialize<List<Feature>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "feature-catalog.json"))) ?? [];
        var search = new TextBox { Width = 500, PlaceholderText = "ابحث عن قسم أو اسم ملف" }; page.Controls.Add(search);
        var grid = Grid(); grid.ReadOnly = true;
        Column(grid, "Module", "القسم"); Column(grid, "Source", "اسم الملف المرجعي"); Column(grid, "Status", "حالة التنفيذ");
        void Bind() => grid.DataSource = features.Where(f => (f.Module + " " + f.Source).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        search.TextChanged += (_, _) => Bind(); page.Controls.Add(grid); Bind();
    }

    private static TextBox Field(Control parent, string title, string value)
    {
        parent.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 8, 0, 3) });
        var field = new TextBox { Text = value, Width = 650 }; parent.Controls.Add(field); return field;
    }

    private void Export(string text, string name)
    {
        using var file = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = name };
        if (file.ShowDialog(this) == DialogResult.OK) File.WriteAllText(file.FileName, text, new UTF8Encoding(true));
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
            Font = new Font("Segoe UI", 10); RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
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
