using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace SenderPro.Core;

public static class ContactWorkbook
{
    public static List<Contact> Read(string path)
    {
        using var document = SpreadsheetDocument.Open(path, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException("ملف Excel لا يحتوي على مصنف.");
        var sheet = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("ملف Excel لا يحتوي على ورقة بيانات.");
        var worksheet = (WorksheetPart)workbook.GetPartById(sheet.Id?.Value ?? throw new InvalidDataException("ورقة Excel غير صالحة."));
        var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToList() ?? [];
        var rows = new List<List<string>>();
        foreach (var row in worksheet.Worksheet.Descendants<Row>())
        {
            var values = new List<string>();
            foreach (var cell in row.Elements<Cell>())
            {
                var column = ColumnIndex(cell.CellReference?.Value ?? throw new InvalidDataException("خلية Excel بلا عنوان."));
                while (values.Count <= column) values.Add("");
                var text = cell.CellValue?.Text ?? cell.InlineString?.InnerText ?? "";
                if (cell.DataType?.Value == CellValues.SharedString)
                {
                    if (!int.TryParse(text, out var index) || index < 0 || index >= strings.Count)
                        throw new InvalidDataException("مرجع نص Excel غير صالح.");
                    text = strings[index].InnerText;
                }
                values[column] = text;
            }
            if (values.Any(v => !string.IsNullOrWhiteSpace(v))) rows.Add(values);
        }
        if (rows.Count == 0) return [];
        var width = rows[0].Count;
        var csv = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.Count > width) throw new InvalidDataException("صف Excel يحتوي على أعمدة إضافية.");
            while (row.Count < width) row.Add("");
            csv.AppendLine(string.Join(",", row.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")));
        }
        return ContactCsv.Read(csv.ToString());
    }

    public static void Write(string path, IEnumerable<Contact> contacts)
    {
        using var document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
        var worksheet = workbook.AddNewPart<WorksheetPart>(); var sheetData = new SheetData();
        worksheet.Worksheet = new Worksheet(sheetData);
        var sheets = workbook.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Contacts" });
        uint rowIndex = 0;
        void AddRow(string[] values)
        {
            var row = new Row { RowIndex = ++rowIndex };
            for (var i = 0; i < values.Length; i++)
                row.Append(new Cell { CellReference = ((char)('A' + i)).ToString() + rowIndex,
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(values[i]) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }) });
            sheetData.Append(row);
        }
        AddRow(["name", "email", "phone", "group", "unsubscribed"]);
        foreach (var c in contacts) AddRow([c.Name, c.Email, c.Phone, c.Group, c.Unsubscribed ? "true" : "false"]);
        workbook.Workbook.Save(); worksheet.Worksheet.Save();
    }

    private static int ColumnIndex(string reference)
    {
        var value = 0;
        foreach (var ch in reference.TakeWhile(char.IsLetter))
            value = checked(value * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
        if (value < 1 || value > 16384) throw new InvalidDataException("عنوان خلية Excel غير صالح.");
        return value - 1;
    }
}
