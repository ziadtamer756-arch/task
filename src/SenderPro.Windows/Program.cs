using SenderPro.Core;

namespace SenderPro.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--ui-smoke-test")
        {
            Directory.CreateDirectory(args[1]);
            var testFolder = Path.Combine(Path.GetTempPath(), "SenderPro-ui-" + Guid.NewGuid());
            try
            {
                var sample = new AppData
                {
                    Contacts = [new() { Name = "زياد", Email = "ziad@example.com", Group = "العملاء" },
                        new() { Name = "أحمد", Email = "ahmed@example.com", Group = "العملاء" }],
                    Campaigns = [new() { Name = "حملة تجريبية — بيانات اختبار", State = "completed", Mode = "preview" }]
                };
                using var form = new MainForm(new DataStore(Path.Combine(testFolder, "data.json")), sample);
                form.RunUiSmoke(args[1]); return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(args[1], "failure.txt"), ex.ToString()); return 1;
            }
            finally { if (Directory.Exists(testFolder)) Directory.Delete(testFolder, true); }
        }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SenderProCommunity");
        using var instance = new Mutex(true, "Local\\SenderProCommunity", out var first);
        if (!first) { MessageBox.Show("البرنامج مفتوح بالفعل."); return 0; }
        try
        {
            var store = new DataStore(Path.Combine(folder, "data.json"));
            Application.Run(new MainForm(store, store.Load()));
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر تشغيل البرنامج. لم يتم استبدال بياناتك.\n" + ex.Message,
                "Sender Pro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        return 0;
    }
}
