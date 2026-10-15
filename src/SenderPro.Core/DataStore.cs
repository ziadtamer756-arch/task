using System.Text.Json;

namespace SenderPro.Core;

public sealed class DataStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath => path;

    public AppData Load()
    {
        if (!File.Exists(path)) return new();
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("ملف البيانات فارغ أو غير صالح.");
        if (data.Contacts is null || data.Templates is null || data.Campaigns is null || data.Smtp is null || data.Messaging is null || data.ContentItems is null)
            throw new InvalidDataException("ملف البيانات لا يحتوي على جميع الأقسام المطلوبة.");
        foreach (var campaign in data.Campaigns.Where(c => c.State == "running"))
            campaign.State = "interrupted";
        return data;
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
        if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
        File.Move(temporary, path, overwrite: true);
    }
}
