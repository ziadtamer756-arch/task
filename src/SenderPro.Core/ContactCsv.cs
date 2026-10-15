using System.Text;

namespace SenderPro.Core;

public static class ContactCsv
{
    public static List<Contact> Read(string text)
    {
        var rows = Parse(text.TrimStart('\uFEFF'));
        if (rows.Count == 0) return [];
        string[] required = ["name", "email", "phone", "group", "unsubscribed"];
        var headers = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        if (headers.Distinct().Count() != headers.Length) throw new InvalidDataException("قائمة الأعمدة تحتوي على أسماء مكررة.");
        if (!required.All(headers.Contains))
            throw new InvalidDataException("أعمدة CSV المطلوبة: name,email,phone,group,unsubscribed");
        var contacts = new List<Contact>();
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Count != headers.Length) throw new InvalidDataException("عدد أعمدة أحد الصفوف غير صحيح.");
            string Get(string key) => row[Array.IndexOf(headers, key)].Trim();
            var unsubscribed = Get("unsubscribed");
            if (unsubscribed != "0" && unsubscribed != "1" &&
                !bool.TryParse(unsubscribed, out _))
                throw new InvalidDataException("unsubscribed يجب أن يكون true/false أو 1/0.");
            contacts.Add(new Contact { Name = Get("name"), Email = Get("email"),
                Phone = Get("phone"), Group = Get("group"),
                TelegramChatId = headers.Contains("telegram_chat_id") ? Get("telegram_chat_id") : "",
                Unsubscribed = unsubscribed == "1" || unsubscribed.Equals("true", StringComparison.OrdinalIgnoreCase) });
        }
        return contacts;
    }

    public static int Merge(List<Contact> existing, IEnumerable<Contact> imported)
    {
        var count = 0;
        foreach (var c in imported)
        {
            var match = existing.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(c.Email) && x.Email.Equals(c.Email, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.Phone) && SocialCampaignRunner.RecipientFor(x, ChannelKind.WhatsApp) == SocialCampaignRunner.RecipientFor(c, ChannelKind.WhatsApp)) ||
                (!string.IsNullOrWhiteSpace(c.TelegramChatId) && x.TelegramChatId.Equals(c.TelegramChatId, StringComparison.OrdinalIgnoreCase)));
            if (match is not null)
            {
                // Importing an older list must never re-subscribe an opted-out contact.
                match.Unsubscribed |= c.Unsubscribed;
                if (string.IsNullOrWhiteSpace(match.TelegramChatId)) match.TelegramChatId = c.TelegramChatId;
                if (string.IsNullOrWhiteSpace(match.Email)) match.Email = c.Email;
                if (string.IsNullOrWhiteSpace(match.Phone)) match.Phone = c.Phone;
                continue;
            }
            existing.Add(c);
            count++;
        }
        return count;
    }

    public static string Write(IEnumerable<Contact> contacts)
    {
        var result = new StringBuilder("name,email,phone,group,unsubscribed,telegram_chat_id\r\n");
        foreach (var c in contacts)
            result.AppendLine(string.Join(",", new[] { c.Name, c.Email, c.Phone, c.Group,
                c.Unsubscribed ? "true" : "false", c.TelegramChatId }.Select(Escape)));
        return result.ToString();
    }

    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch != '"') { cell.Append(ch); continue; }
                if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else { quoted = false; closedQuote = true; }
                continue;
            }
            if (ch == '"')
            {
                if (cell.Length > 0 || closedQuote) throw new InvalidDataException("علامة اقتباس CSV في موضع غير صحيح.");
                quoted = true;
            }
            else if (ch == ',' || ch == '\r' || ch == '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); closedQuote = false;
                if (ch != ',')
                {
                    rows.Add(row); row = [];
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
            }
            else
            {
                if (closedQuote) throw new InvalidDataException("نص غير صالح بعد نهاية اقتباس CSV.");
                cell.Append(ch);
            }
        }
        if (quoted) throw new InvalidDataException("اقتباس CSV غير مغلق.");
        if (cell.Length > 0 || row.Count > 0 || closedQuote) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
