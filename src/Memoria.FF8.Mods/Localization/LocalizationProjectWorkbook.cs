using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

internal static class LocalizationProjectWorkbook
{
    private const long MaximumExpandedWorkbookBytes = LocalizationProjectIO.MaximumDocumentBytes;

    public static void Write(string path, IReadOnlyList<LocalizationRecord> rows, LocalizationManifest manifest, LocalizationPayload payload)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbook = document.AddWorkbookPart();
        workbook.Workbook = new Workbook(new BookViews(new WorkbookView()));
        WorkbookStylesPart styles = workbook.AddNewPart<WorkbookStylesPart>();
        styles.Stylesheet = new Stylesheet(
            new Fonts(new Font(), new Font(new Bold())),
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }), new Fill(new PatternFill(new ForegroundColor { Rgb = "FFF0F7E8" }) { PatternType = PatternValues.Solid })),
            new Borders(new Border()),
            new CellStyleFormats(new CellFormat()),
            new CellFormats(new CellFormat(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top }), new CellFormat(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top }) { FillId = 2, ApplyFill = true }, new CellFormat { FontId = 1, ApplyFont = true }));
        Sheets sheets = workbook.Workbook.AppendChild(new Sheets());
        uint sheetId = 1;
        foreach (IGrouping<string, LocalizationRecord> category in rows.GroupBy(row => row.Category, StringComparer.Ordinal))
        {
            string name = payload.Worksheets[category.Key];
            if (name.Length > 31 || name.Length == 0 || name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0 || name.StartsWith('\'') || name.EndsWith('\''))
            {
                throw new InvalidDataException($"Category {category.Key} requires an explicit Excel-safe worksheet mapping.");
            }
            string[] extraColumns = category.SelectMany(row => row.ExtensionData).Where(pair => pair.Value.ValueKind == JsonValueKind.String).Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] headers = ["File", "Key", manifest.SourceColumn, manifest.TargetColumn, .. manifest.ReferenceLanguages.Keys, .. extraColumns];
            if (headers.Length > 16384 || category.Count() > 1048575)
            {
                throw new InvalidDataException($"Category {category.Key} exceeds Excel's row or column limits.");
            }
            WorksheetPart part = workbook.AddNewPart<WorksheetPart>();
            SheetData data = new();
            Columns columns = new();
            for (int i = 0; i < headers.Length; i++)
            {
                columns.Append(new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = i < 2 ? 30 : 55, CustomWidth = true });
            }
            part.Worksheet = new Worksheet(new SheetViews(new SheetView(new Pane { HorizontalSplit = 2, VerticalSplit = 1, TopLeftCell = "C2", ActivePane = PaneValues.BottomRight, State = PaneStateValues.Frozen }) { WorkbookViewId = 0 }), columns, data);
            Row header = new() { RowIndex = 1 };
            for (int i = 0; i < headers.Length; i++)
            {
                header.Append(MakeCell(headers[i], ColumnName(i) + "1", "header", 2));
            }
            data.Append(header);
            uint rowIndex = 2;
            foreach (LocalizationRecord row in category)
            {
                List<string?> values = [row.File, row.Key, row.Source, row.Target];
                values.AddRange(manifest.ReferenceLanguages.Keys.Select(key => row.References.GetValueOrDefault(key)));
                values.AddRange(extraColumns.Select(key => row.ExtensionData.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null));
                Row output = new() { RowIndex = rowIndex };
                for (int i = 0; i < values.Count; i++)
                {
                    output.Append(MakeCell(values[i] ?? "", ColumnName(i) + rowIndex, row.Key, i == 3 ? 1U : 0U));
                }
                data.Append(output);
                rowIndex++;
            }
            part.Worksheet.Append(new AutoFilter { Reference = "A1:" + ColumnName(headers.Length - 1) + (rowIndex - 1) });
            part.Worksheet.Save();
            sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = sheetId++, Name = name });
        }
        styles.Stylesheet.Save();
        workbook.Workbook.Save();
    }

    public static IReadOnlyList<LocalizationRecord> Read(byte[] bytes, LocalizationManifest manifest, LocalizationPayload payload)
    {
        ValidateZip(bytes);
        using MemoryStream snapshot = new(bytes, false);
        using SpreadsheetDocument document = SpreadsheetDocument.Open(snapshot, false, new OpenSettings { AutoSave = false, MaxCharactersInPart = MaximumExpandedWorkbookBytes });
        ValidationErrorInfo? packageError = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document).FirstOrDefault();
        if (packageError is not null)
        {
            throw new InvalidDataException($"Invalid Open XML workbook: {packageError.Description}");
        }
        WorkbookPart workbook = document.WorkbookPart ?? throw new InvalidDataException("Workbook has no workbook part.");
        Dictionary<string, string> categories = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string category, string worksheet) in payload.Worksheets)
        {
            if (!categories.TryAdd(worksheet, category))
            {
                throw new InvalidDataException("Ambiguous category-to-worksheet mapping.");
            }
        }
        if (categories.Count == 0)
        {
            throw new InvalidDataException("The manifest must declare workbook worksheet/category identities.");
        }
        SharedStringItem[] shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToArray() ?? [];
        List<LocalizationRecord> result = [];
        HashSet<string> seenSheets = new(StringComparer.OrdinalIgnoreCase);
        foreach (Sheet sheet in workbook.Workbook?.Sheets?.Elements<Sheet>() ?? [])
        {
            string name = sheet.Name?.Value ?? "";
            if (!categories.TryGetValue(name, out string? category) || !seenSheets.Add(name))
            {
                throw new InvalidDataException($"Unknown or duplicate localization worksheet: {name}.");
            }
            WorksheetPart part = (WorksheetPart)workbook.GetPartById(sheet.Id?.Value ?? throw new InvalidDataException("Missing worksheet relationship."));
            Worksheet worksheet = part.Worksheet ?? throw new InvalidDataException("Missing worksheet content.");
            if (worksheet.Descendants<MergeCell>().Any())
            {
                throw new InvalidDataException($"Merged data cells are not supported in {name}.");
            }
            Row[] rows = worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? [];
            Row header = rows.FirstOrDefault(row => row.RowIndex?.Value == 1) ?? throw new InvalidDataException($"Worksheet {name} has no header row.");
            Dictionary<int, Cell> headerCells = Cells(header);
            Dictionary<string, int> headers = new(StringComparer.Ordinal);
            foreach ((int index, Cell cell) in headerCells)
            {
                if (cell.CellFormula is not null)
                {
                    throw new InvalidDataException($"Formula in localization header {name}!{cell.CellReference}.");
                }
                string value = CellText(cell, shared);
                if (!string.IsNullOrEmpty(value) && !headers.TryAdd(value, index))
                {
                    throw new InvalidDataException($"Duplicate workbook column: {value}.");
                }
            }
            string[] required = ["File", "Key", manifest.SourceColumn, manifest.TargetColumn, .. manifest.ReferenceLanguages.Keys];
            foreach (string label in required)
            {
                if (!headers.ContainsKey(label))
                {
                    throw new InvalidDataException($"Worksheet {name} is missing column {label}.");
                }
            }
            HashSet<uint> rowNumbers = [];
            foreach (Row row in rows)
            {
                uint rowNumber = row.RowIndex?.Value ?? throw new InvalidDataException("Missing worksheet row address.");
                if (!rowNumbers.Add(rowNumber))
                {
                    throw new InvalidDataException($"Duplicate worksheet row {rowNumber}.");
                }
                if (rowNumber == 1)
                {
                    continue;
                }
                Dictionary<int, Cell> cells = Cells(row);
                if (cells.Values.All(cell => string.IsNullOrEmpty(CellText(cell, shared))))
                {
                    continue;
                }
                string Value(string label)
                {
                    if (!cells.TryGetValue(headers[label], out Cell? cell))
                    {
                        return "";
                    }
                    if (cell.CellFormula is not null)
                    {
                        throw new InvalidDataException($"Formula in {name}!{cell.CellReference}: {label}; required identity and translation cells must be literal strings.");
                    }
                    return CellText(cell, shared);
                }
                string file = Value("File");
                string key = Value("Key");
                if ((payload.Category is not null && payload.Category != category) || (payload.File is not null && payload.File != file))
                {
                    throw new InvalidDataException($"Workbook {payload.Path} contains unrelated file {file}.");
                }
                Dictionary<string, JsonElement> extra = new(StringComparer.Ordinal);
                foreach ((string label, int index) in headers.Where(pair => !required.Contains(pair.Key, StringComparer.Ordinal)))
                {
                    if (cells.TryGetValue(index, out Cell? cell))
                    {
                        if (cell.CellFormula is not null)
                        {
                            throw new InvalidDataException($"Formula authoring extension cannot be converted losslessly: {name}!{cell.CellReference}.");
                        }
                        extra[label] = ExtensionValue(cell, shared);
                    }
                }
                result.Add(new LocalizationRecord
                {
                    Category = category,
                    File = file,
                    Key = key,
                    Source = Value(manifest.SourceColumn),
                    SourcePresent = !string.IsNullOrEmpty(Value(manifest.SourceColumn)),
                    Target = Value(manifest.TargetColumn),
                    References = manifest.ReferenceLanguages.Keys.ToDictionary(label => label, label => (string?)Value(label)),
                    ReferencePresence = manifest.ReferenceLanguages.Keys.ToDictionary(label => label, label => !string.IsNullOrEmpty(Value(label))),
                    ExtensionData = extra
                });
            }
        }
        if (seenSheets.Count != categories.Count)
        {
            throw new InvalidDataException("A declared localization worksheet is missing.");
        }
        return result;
    }

    private static Cell MakeCell(string value, string address, string key, uint style)
    {
        if (value.Length > 32767 || value.Count(character => character == '\n') > 253)
        {
            throw new InvalidDataException($"{key}: cell {address} exceeds Excel's 32,767-character or 253-line-feed limit.");
        }
        string encoded = Regex.Replace(value, "_x[0-9a-fA-F]{4}_", match => "_x005F_" + match.Value[1..], RegexOptions.CultureInvariant);
        encoded = Regex.Replace(encoded, "[\\x00-\\x08\\x0B\\x0C\\x0D\\x0E-\\x1F]", match => "_x" + ((int)match.Value[0]).ToString("X4", CultureInfo.InvariantCulture) + "_", RegexOptions.CultureInvariant);
        return new Cell(new InlineString(new Text(encoded) { Space = SpaceProcessingModeValues.Preserve })) { CellReference = address, DataType = CellValues.InlineString, StyleIndex = style };
    }

    private static string CellText(Cell cell, SharedStringItem[] shared)
    {
        string text;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (!int.TryParse(cell.CellValue?.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index < 0 || index >= shared.Length)
            {
                throw new InvalidDataException($"Invalid shared-string index at {cell.CellReference}.");
            }
            text = string.Concat(shared[index].Descendants<Text>().Where(item => !item.Ancestors<PhoneticRun>().Any()).Select(item => item.Text));
        }
        else if (cell.DataType?.Value == CellValues.InlineString)
        {
            text = string.Concat(cell.InlineString?.Descendants<Text>().Where(item => !item.Ancestors<PhoneticRun>().Any()).Select(item => item.Text) ?? []);
        }
        else
        {
            text = cell.CellValue?.Text ?? "";
        }
        return Regex.Replace(text, "_x([0-9a-fA-F]{4})_", match => ((char)ushort.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(), RegexOptions.CultureInvariant);
    }

    private static JsonElement ExtensionValue(Cell cell, SharedStringItem[] shared)
    {
        string text = CellText(cell, shared);
        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return text switch { "0" => JsonSerializer.SerializeToElement(false), "1" => JsonSerializer.SerializeToElement(true), _ => throw new InvalidDataException($"Invalid Boolean authoring value at {cell.CellReference}.") };
        }
        if ((cell.DataType is null || cell.DataType.Value == CellValues.Number) && text.Length > 0)
        {
            try
            {
                using JsonDocument number = JsonDocument.Parse(text);
                if (number.RootElement.ValueKind == JsonValueKind.Number)
                {
                    return number.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                throw new InvalidDataException($"Invalid numeric authoring value at {cell.CellReference}.");
            }
        }
        return JsonSerializer.SerializeToElement(text);
    }

    private static Dictionary<int, Cell> Cells(Row row)
    {
        Dictionary<int, Cell> result = [];
        foreach (Cell cell in row.Elements<Cell>())
        {
            string address = cell.CellReference?.Value ?? throw new InvalidDataException("Missing worksheet cell address.");
            Match match = Regex.Match(address, "^([A-Z]{1,3})([1-9][0-9]*)$", RegexOptions.CultureInvariant);
            if (!match.Success || match.Groups[2].Value != row.RowIndex?.Value.ToString(CultureInfo.InvariantCulture))
            {
                throw new InvalidDataException($"Invalid worksheet cell address: {address}.");
            }
            int column = 0;
            foreach (char character in match.Groups[1].Value)
            {
                column = column * 26 + character - 'A' + 1;
            }
            if (column > 16384 || !result.TryAdd(column - 1, cell))
            {
                throw new InvalidDataException($"Duplicate or out-of-range cell address: {address}.");
            }
        }
        return result;
    }

    private static string ColumnName(int index)
    {
        string name = "";
        do
        {
            name = (char)('A' + index % 26) + name;
            index = index / 26 - 1;
        }
        while (index >= 0);
        return name;
    }

    private static void ValidateZip(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, false);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        long total = 0;
        long actualTotal = 0;
        byte[] buffer = new byte[64 * 1024];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"Workbook contains a duplicate ZIP entry: {entry.FullName}.");
            }
            long length = entry.Length;
            if (length < 0 || length > MaximumExpandedWorkbookBytes - total)
            {
                throw new InvalidDataException($"Workbook decompressed content exceeds the {MaximumExpandedWorkbookBytes} byte limit.");
            }
            total += length;
            using Stream content = entry.Open();
            long actual = 0;
            int count;
            while ((count = content.Read(buffer)) != 0)
            {
                if (count > length - actual)
                {
                    throw new InvalidDataException("Workbook ZIP entry exceeds its declared length.");
                }
                if (count > MaximumExpandedWorkbookBytes - actualTotal)
                {
                    throw new InvalidDataException($"Workbook decompressed content exceeds the {MaximumExpandedWorkbookBytes} byte limit.");
                }
                actual += count;
                actualTotal += count;
            }
            if (actual != length)
            {
                throw new InvalidDataException("Workbook ZIP entry does not match its declared length.");
            }
        }
    }
}
