using SenderPro.Core;

namespace SenderPro.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SenderProCommunity");
        using var instance = new Mutex(true, "Local\\SenderProCommunity", out var first);
        if (!first) { MessageBox.Show("البرنامج مفتوح بالفعل."); return; }
        try
        {
            var store = new DataStore(Path.Combine(folder, "data.json"));
            Application.Run(new MainForm(store, store.Load()));
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر تشغيل البرنامج. لم يتم استبدال بياناتك.\n" + ex.Message,
                "Sender Pro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
