using System.Text;
using SenderPro.Core;

namespace SenderPro.Windows;

public sealed partial class MainForm
{
    private void ContentLibrary()
    {
        var page = Page("مكتبة المحتوى", "احفظ النصوص والوسوم والملفات المرتبطة لإعادة استخدامها في حملاتك.");
        var search = Field(page, "البحث في الاسم أو النص أو الوسوم", "");
        search.PlaceholderText = "اكتب كلمة للبحث…";
        var summary = new Label { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 12) };
        var grid = Grid();
        grid.Height = 310; grid.ReadOnly = true;
        Column(grid, nameof(ContentLibraryRow.Name), "الاسم");
        Column(grid, nameof(ContentLibraryRow.Tags), "الوسوم");
        Column(grid, nameof(ContentLibraryRow.MediaCount), "الملفات");
        Column(grid, nameof(ContentLibraryRow.FilesStatus), "حالة الملفات");
        Column(grid, nameof(ContentLibraryRow.UpdatedAt), "آخر تعديل");
        grid.Columns[2].FillWeight = 50;
        var preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Height = 120, Width = 850, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 12) };
        var files = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
            WordWrap = false, Height = 90, Width = 850, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 12) };

        ContentItem? Selected() => (grid.CurrentRow?.DataBoundItem as ContentLibraryRow)?.Item;
        void UpdatePreview()
        {
            var item = Selected();
            preview.Text = item?.Text ?? "";
            files.Text = item is null ? "اختر محتوى لمراجعة ملفاته." : item.MediaPaths.Count == 0
                ? "لا توجد ملفات مرتبطة بهذا المحتوى."
                : string.Join(Environment.NewLine, item.MediaPaths.Select(path =>
                    $"{(File.Exists(path) ? "موجود" : "مفقود — أعد ربطه من التعديل")}  |  {path}"));
        }
        void Bind(Guid? select = null)
        {
            var target = select ?? Selected()?.Id;
            var query = search.Text.Trim();
            var items = data.ContentItems.Where(item => query.Length == 0 ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Tags.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.UpdatedAt).Select(item => new ContentLibraryRow(item)).ToList();
            grid.DataSource = items;
            foreach (DataGridViewRow row in grid.Rows)
                if (row.DataBoundItem is ContentLibraryRow bound && bound.Item.Id == target)
                { grid.CurrentCell = row.Cells[0]; break; }
            summary.Text = $"{items.Count} نتيجة من {data.ContentItems.Count} محتوى محفوظ";
            UpdatePreview();
        }
        void Edit(bool add)
        {
            var original = add ? null : Selected();
            if (!add && original is null) return;
            using var editor = new ContentLibraryEditor(original);
            if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is not { } updated) return;
            if (add)
            {
                data.ContentItems.Add(updated);
                try { Save(); } catch { data.ContentItems.Remove(updated); throw; }
            }
            else
            {
                var index = data.ContentItems.IndexOf(original!);
                if (index < 0) throw new InvalidOperationException("لم يعد المحتوى المحدد موجودًا. أعد فتح المكتبة.");
                data.ContentItems[index] = updated;
                try { Save(); } catch { data.ContentItems[index] = original!; throw; }
            }
            Bind(updated.Id);
        }
        void ImportTextFiles()
        {
            using var file = new OpenFileDialog { Filter = "ملفات نصية UTF-8|*.txt", Multiselect = true,
                Title = "إضافة ملفات نصية إلى مكتبة المحتوى" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            var imported = new List<ContentItem>();
            var errors = new List<string>();
            var duplicates = 0;
            foreach (var path in file.FileNames)
            {
                try
                {
                    if (!Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("يجب اختيار ملف بامتداد .txt.");
                    if (new FileInfo(path).Length > 2 * 1024 * 1024)
                        throw new InvalidDataException("الحد الأقصى للملف النصي 2 ميجابايت.");
                    var bytes = File.ReadAllBytes(path);
                    var offset = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
                    var text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
                    if (text.IndexOf('\0') >= 0) throw new InvalidDataException("الملف يحتوي على بيانات غير نصية؛ احفظه بترميز UTF-8.");
                    if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("الملف فارغ.");
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (data.ContentItems.Concat(imported).Any(item => item.Name == name && item.Text == text))
                    { duplicates++; continue; }
                    imported.Add(new ContentItem { Name = name, Text = text });
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or DecoderFallbackException or ArgumentException)
                { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
            }
            data.ContentItems.AddRange(imported);
            try { Save(); }
            catch { foreach (var item in imported) data.ContentItems.Remove(item); throw; }
            Bind(imported.FirstOrDefault()?.Id);
            var message = $"أُضيفت {imported.Count} مادة إلى المكتبة. تم تجاوز {duplicates} ملف مكرر.";
            if (errors.Count > 0) message += $"\nتعذر استيراد {errors.Count} ملف:\n" +
                string.Join("\n", errors.Take(8)) + (errors.Count > 8 ? "\n…" : "");
            MessageBox.Show(this, message, "استيراد المحتوى", MessageBoxButtons.OK,
                errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        var bar = Toolbar();
        bar.Controls.Add(Button("إضافة محتوى", (_, _) => Guard(() => Edit(true))));
        bar.Controls.Add(Button("تعديل المحدد", (_, _) => Guard(() => Edit(false))));
        bar.Controls.Add(Button("حذف المحدد", (_, _) => Guard(() =>
        {
            if (Selected() is not { } selected) return;
            if (MessageBox.Show(this, $"حذف «{selected.Name}» من المكتبة؟ ستبقى الملفات الأصلية على جهازك.",
                "حذف المحتوى", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var index = data.ContentItems.IndexOf(selected);
            data.ContentItems.Remove(selected);
            try { Save(); } catch { data.ContentItems.Insert(index, selected); throw; }
            Bind();
        })));
        bar.Controls.Add(Button("استيراد ملفات نصية", (_, _) => Guard(ImportTextFiles)));
        bar.Controls.Add(Button("إنشاء قالب من النص فقط", (_, _) => Guard(() =>
        {
            if (Selected() is not { } selected) return;
            if (string.IsNullOrWhiteSpace(selected.Text))
                throw new InvalidDataException("المحتوى المحدد لا يحتوي على نص لإنشاء قالب.");
            using var dialog = new Editor("إنشاء قالب من النص فقط", ("اسم القالب", selected.Name), ("عنوان البريد (اختياري)", ""));
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var name = dialog.Value(0).Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("أدخل اسم القالب.");
            var template = new MessageTemplate { Name = name, Subject = dialog.Value(1), Body = selected.Text };
            data.Templates.Add(template);
            try { Save(); } catch { data.Templates.Remove(template); throw; }
            status.Text = $"أُنشئ القالب «{name}» من النص فقط. الملفات المرتبطة لم تُضف إلى القالب.";
        })));
        page.Controls.Add(bar); page.Controls.Add(summary); page.Controls.Add(grid);
        page.Controls.Add(new Label { Text = "معاينة النص", AutoSize = true, ForeColor = Theme.Ink,
            Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 4, 0, 8) });
        page.Controls.Add(preview);
        page.Controls.Add(new Label { Text = "الملفات المرتبطة وحالتها", AutoSize = true, ForeColor = Theme.Ink,
            Margin = new Padding(0, 4, 0, 8) });
        page.Controls.Add(files);
        search.TextChanged += (_, _) => Bind();
        grid.SelectionChanged += (_, _) => UpdatePreview();
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Guard(() => Edit(false)); };
        Bind();
    }

    private sealed class ContentLibraryRow(ContentItem item)
    {
        public ContentItem Item { get; } = item;
        public string Name => Item.Name;
        public string Tags => Item.Tags;
        public int MediaCount => Item.MediaPaths.Count;
        public string FilesStatus
        {
            get
            {
                if (MediaCount == 0) return "نص فقط";
                var missing = Item.MediaPaths.Count(path => !File.Exists(path));
                return missing > 0 ? $"{missing} ملف مفقود" : "كل الملفات موجودة";
            }
        }
        public string UpdatedAt => Item.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
    }

    private sealed class ContentLibraryEditor : Form
    {
        public ContentItem? Result { get; private set; }

        public ContentLibraryEditor(ContentItem? original)
        {
            Text = original is null ? "محتوى جديد" : "تعديل المحتوى";
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
            Size = new Size(Math.Min(820, workingArea.Width - 50), Math.Min(800, workingArea.Height - 60));
            MinimumSize = new Size(560, 500); StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = false;
            Font = new Font("Segoe UI", 10); BackColor = Theme.Background; ForeColor = Theme.Ink;
            ShowInTaskbar = false; MinimizeBox = false;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            var page = new ResponsivePage();
            scroll.Controls.Add(page);
            var commands = Toolbar(); commands.Dock = DockStyle.Bottom;
            commands.Padding = new Padding(16, 8, 16, 8); commands.BackColor = Color.White;
            Controls.Add(scroll); Controls.Add(commands);
            page.Controls.Add(new Label { Text = Text, AutoSize = true, Font = new Font(Font, FontStyle.Bold),
                ForeColor = Theme.Ink, Margin = new Padding(0, 0, 0, 12) });
            var name = Field(page, "اسم المحتوى", original?.Name ?? "");
            var tags = Field(page, "الوسوم — افصلها بفواصل", original?.Tags ?? "");
            var text = Field(page, "النص", original?.Text ?? "");
            text.Multiline = true; text.AcceptsReturn = true; text.ScrollBars = ScrollBars.Vertical;
            text.Height = 190; text.MaxLength = 2 * 1024 * 1024;
            var mediaPaths = original?.MediaPaths.ToList() ?? [];
            var media = new ListBox { Width = 650, Height = 145, HorizontalScrollbar = true,
                BackColor = Color.White, ForeColor = Theme.Ink, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10) };
            var mediaInfo = new Label { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 10) };
            void BindMedia()
            {
                var index = media.SelectedIndex;
                media.DataSource = mediaPaths.Select(path => $"{(File.Exists(path) ? "موجود" : "مفقود")}  |  {path}").ToList();
                if (media.Items.Count > 0) media.SelectedIndex = Math.Clamp(index, 0, media.Items.Count - 1);
                var missing = mediaPaths.Count(path => !File.Exists(path));
                mediaInfo.Text = $"{mediaPaths.Count} ملف مرتبط · {missing} مفقود. تُحفظ المسارات؛ احتفظ بالملفات في أماكنها.";
            }
            var mediaButtons = Toolbar();
            mediaButtons.Controls.Add(Button("إضافة ملفات", (_, _) => Guard(() =>
            {
                using var dialog = new OpenFileDialog { Multiselect = true, Title = "ربط ملفات بالمحتوى",
                    Filter = "صور وفيديو وصوت|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.mp4;*.mov;*.webm;*.mp3;*.wav;*.ogg|كل الملفات|*.*" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                foreach (var path in dialog.FileNames)
                    if (!mediaPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) mediaPaths.Add(path);
                BindMedia();
            })));
            mediaButtons.Controls.Add(Button("إزالة الرابط المحدد", (_, _) =>
            {
                if (media.SelectedIndex < 0) return;
                mediaPaths.RemoveAt(media.SelectedIndex); BindMedia();
            }));
            page.Controls.Add(new Label { Text = "الملفات المرتبطة", AutoSize = true,
                Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 12, 0, 8) });
            page.Controls.Add(mediaButtons); page.Controls.Add(media); page.Controls.Add(mediaInfo);
            var save = Button("حفظ المحتوى", (_, _) => Guard(() =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidDataException("أدخل اسم المحتوى.");
                if (string.IsNullOrWhiteSpace(text.Text) && mediaPaths.Count == 0)
                    throw new InvalidDataException("أضف نصًا أو ملفًا مرتبطًا إلى المحتوى.");
                Result = new ContentItem { Id = original?.Id ?? Guid.NewGuid(), Name = name.Text.Trim(),
                    Text = text.Text, Tags = tags.Text.Trim(), MediaPaths = mediaPaths.ToList(), UpdatedAt = DateTimeOffset.UtcNow };
                DialogResult = DialogResult.OK;
            }));
            var cancel = Button("إلغاء", (_, _) => { DialogResult = DialogResult.Cancel; });
            cancel.DialogResult = DialogResult.Cancel;
            commands.Controls.Add(save); commands.Controls.Add(cancel); CancelButton = cancel;
            Shown += (_, _) => { page.Fit(); name.Focus(); };
            BindMedia();
        }
    }
}
