using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationReferenceWorkbookTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memoria-reference-workbook-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EntirelyAbsentReferencesExplainTheirAbsenceWithoutInventingText()
    {
        var project = Project(2);
        var row = project.Entries.Single() with
        {
            References = new Dictionary<string, string?> { ["Reference [jp]"] = null, ["Reference [en]"] = "English original" },
            ReferencePresence = new Dictionary<string, bool> { ["Reference [jp]"] = false, ["Reference [en]"] = true },
            ReferenceKeys = new Dictionary<string, IReadOnlyList<string>> { ["Reference [jp]"] = Array.Empty<string>(), ["Reference [en]"] = new[] { "$en_scene_0000" } }
        };
        string directory = Path.Combine(_root, "unavailable");
        LocalizationProjectIO.WriteNew(project with { Entries = new[] { row } }, directory, "xlsx", "single", TestContext.Current.CancellationToken);
        using (var workbook = SpreadsheetDocument.Open(Path.Combine(directory, "Localization/localization.xlsx"), false))
        {
            var note = Assert.Single(workbook.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<DataValidation>());
            Assert.Equal("D2:D2", note.SequenceOfReferences!.InnerText);
            Assert.True(note.ShowInputMessage!.Value);
            Assert.Contains("no matching original keys", note.Prompt!.Value, StringComparison.Ordinal);
        }
        var loaded = Assert.Single(LocalizationProjectIO.Read(directory).Entries);
        Assert.Null(loaded.References["Reference [jp]"]);
        Assert.Empty(loaded.ReferenceKeys["Reference [jp]"]);
    }

    [Fact]
    public void ReferenceLayoutKeepsTranslatorColumnsAndTechnicalSidecarDistinct()
    {
        LocalizationProject project = Project(2);
        string directory = Path.Combine(_root, "reference");
        LocalizationProjectIO.WriteNew(project, directory, "xlsx", "single", TestContext.Current.CancellationToken);
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(Path.Combine(directory, "Localization/localization.xlsx"), false))
        {
            Row[] rows = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
            Assert.Equal(["File", "Key", "Target", "Reference [jp]", "Reference [en]", "ReferenceKey [jp]", "ReferenceKey [en]", "Field Name"], rows[0].Elements<Cell>().Select(cell => cell.InnerText));
            Cell[] cells = rows[1].Elements<Cell>().ToArray();
            Assert.Equal("C2", cells[2].CellReference!.Value);
            Assert.Equal(1U, cells[2].StyleIndex!.Value);
            Assert.Equal("$jp_scene_0000", cells[5].InnerText);
            Assert.Equal("[\"$en_scene_0000\",\"$en_scene_0001\"]", cells[6].InnerText);
            Assert.DoesNotContain(rows[0].Elements<Cell>(), cell => cell.InnerText.Contains("Source", StringComparison.Ordinal) || cell.InnerText.Contains("Hash", StringComparison.Ordinal) || cell.InnerText.Contains("Ordinal", StringComparison.Ordinal));
        }
        LocalizationRecord actual = LocalizationProjectIO.Read(directory).Entries.Single();
        LocalizationRecord expected = project.Entries.Single();
        Assert.Equal(expected.Source, actual.Source);
        foreach ((string label, string? value) in expected.References)
        {
            Assert.Equal(value, actual.References[label]);
        }
        Assert.Equal(expected.ReferenceKeys["Reference [jp]"], actual.ReferenceKeys["Reference [jp]"]);
        Assert.Equal(expected.ReferenceKeys["Reference [en]"], actual.ReferenceKeys["Reference [en]"]);
        foreach ((string key, JsonElement value) in expected.ExtensionData)
        {
            Assert.True(JsonElement.DeepEquals(value, actual.ExtensionData[key]));
        }
        string json = Path.Combine(_root, "json");
        string restored = Path.Combine(_root, "restored");
        LocalizationProjectIO.Convert(directory, json, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectIO.Convert(json, restored, "xlsx", "single", TestContext.Current.CancellationToken);
        LocalizationRecord roundTrip = LocalizationProjectIO.Read(restored).Entries.Single();
        Assert.Equal(expected.Source, roundTrip.Source);
        Assert.Equal(expected.ReferenceKeys["Reference [en]"], roundTrip.ReferenceKeys["Reference [en]"]);
    }

    [Fact]
    public void LegacyWorkbookRetainsSourceAndStringExtensions()
    {
        LocalizationProject project = Project(1);
        string directory = Path.Combine(_root, "legacy");
        LocalizationProjectIO.WriteNew(project, directory, "xlsx", "single", TestContext.Current.CancellationToken);
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(Path.Combine(directory, "Localization/localization.xlsx"), false))
        {
            Row header = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().First();
            Assert.Equal(["File", "Key", "Source [en]", "Target", "Reference [jp]", "Reference [en]", "Field Name", "Resource Hash"], header.Elements<Cell>().Select(cell => cell.InnerText));
        }
        LocalizationRecord actual = LocalizationProjectIO.Read(directory).Entries.Single();
        Assert.Equal(project.Entries.Single().Source, actual.Source);
        Assert.Equal("synthetic", actual.ExtensionData["Resource Hash"].GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceKeyHeadersAreRequiredAndLiteral(bool formula)
    {
        string directory = Path.Combine(_root, "invalid-key");
        LocalizationProjectIO.WriteNew(Project(2), directory, "xlsx", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "Localization/localization.xlsx");
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, true))
        {
            Worksheet worksheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            if (formula)
            {
                worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "F2").CellFormula = new CellFormula("1+1");
            }
            else
            {
                worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "F1").Remove();
            }
            worksheet.Save();
        }
        string message = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory)).Message;
        Assert.Contains(formula ? "Formula" : "ReferenceKey [jp]", message);
    }

    [Theory]
    [InlineData("json", false)]
    [InlineData("xlsx", false)]
    [InlineData("json", true)]
    [InlineData("xlsx", true)]
    public void ReferenceLayoutRequiresNativeBindingSidecar(string format, bool removeFile)
    {
        string directory = Path.Combine(_root, "missing-binding");
        LocalizationProjectIO.WriteNew(Project(2), directory, format, "single", TestContext.Current.CancellationToken);
        string sidecar = Path.Combine(directory, "Localization/localization.extensions.json");
        if (removeFile)
        {
            File.Delete(sidecar);
            Assert.Throws<FileNotFoundException>(() => LocalizationProjectIO.Read(directory));
        }
        else
        {
            File.WriteAllText(sidecar, "{}");
            Assert.Contains("Missing native binding metadata", Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory)).Message);
        }
    }

    [Theory]
    [InlineData("json", "single", "xlsx", "split")]
    [InlineData("xlsx", "split", "json", "single")]
    public void PatchEditsAndHiddenBaseControlsSurviveFormatAndLayoutConversion(string firstFormat, string firstLayout, string secondFormat, string secondLayout)
    {
        LocalizationProject source = Project(2);
        source = source with
        {
            Entries = [source.Entries.Single() with
            {
                Source = "Base {Var 1}",
                Target = "Initial {Var 1}",
                References = new Dictionary<string, string?> { ["Reference [jp]"] = "Reference {Var 2}", ["Reference [en]"] = "Base {Var 1}" }
            }]
        };
        LocalizationProject patch = LocalizationProjectExport.CreatePatch(source, "Synthetic patch");
        Assert.Equal("", patch.Entries.Single().Target);
        Assert.Equal(source.Entries.Single().ReferenceKeys["Reference [jp]"], patch.Entries.Single().ReferenceKeys["Reference [jp]"]);
        string directory = Path.Combine(_root, "patch");
        LocalizationProjectIO.WriteNew(patch, directory, firstFormat, firstLayout, TestContext.Current.CancellationToken);
        LocalizationProject written = LocalizationProjectIO.Read(directory);
        string payloadPath = Path.Combine(directory, written.Manifest.Payloads.Single().Path);
        const string editedTarget = "Translated {Var 1}";
        const string editedReferenceKey = "$jp_scene_0002";
        if (firstFormat == "json")
        {
            JsonNode payload = JsonNode.Parse(File.ReadAllText(payloadPath))!;
            JsonObject row = payload["Fields"]!["scene"]!["$en_scene_0000"]!.AsObject();
            Assert.False(row.ContainsKey(written.Manifest.SourceColumn));
            row["Target"] = editedTarget;
            row["ReferenceKey [jp]"] = editedReferenceKey;
            File.WriteAllText(payloadPath, payload.ToJsonString());
        }
        else
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Open(payloadPath, true);
            Worksheet worksheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            Assert.DoesNotContain(worksheet.GetFirstChild<SheetData>()!.Elements<Row>().First().Elements<Cell>(), cell => cell.InnerText == written.Manifest.SourceColumn);
            worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "C2").InlineString = new InlineString(new Text(editedTarget));
            worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "F2").InlineString = new InlineString(new Text(editedReferenceKey));
            worksheet.Save();
        }
        string converted = Path.Combine(_root, "converted-patch");
        string restored = Path.Combine(_root, "restored-patch");
        LocalizationProjectIO.Convert(directory, converted, secondFormat, secondLayout, TestContext.Current.CancellationToken);
        LocalizationProjectIO.Convert(converted, restored, firstFormat, firstLayout, TestContext.Current.CancellationToken);
        foreach (string path in new[] { directory, converted, restored })
        {
            LocalizationRecord row = LocalizationProjectIO.Read(path).Entries.Single();
            Assert.Equal(editedTarget, row.Target);
            Assert.Equal([editedReferenceKey], row.ReferenceKeys["Reference [jp]"]);
            Assert.Equal("Base {Var 1}", row.Source);
            Assert.Equal("Reference {Var 2}", row.References["Reference [jp]"]);
            LocalizationTextValidation.Validate(row);
            Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "Translated {Var 2}" }));
        }
    }

    [Fact]
    public void BlankFieldNameCellsPreserveNullMetadataAndClearExistingNames()
    {
        LocalizationProject project = Project(2);
        LocalizationRecord template = project.Entries.Single();
        Dictionary<string, JsonElement> nullMetadata = template.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        nullMetadata["Field Name"] = JsonSerializer.SerializeToElement<string?>(null);
        project = project with
        {
            Entries = [template with { ExtensionData = nullMetadata }, template with { Key = "$en_scene_0001" }]
        };
        string directory = Path.Combine(_root, "blank-field-names");
        LocalizationProjectIO.WriteNew(project, directory, "xlsx", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "Localization/localization.xlsx");
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, true))
        {
            Worksheet worksheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "H3").InlineString = new InlineString(new Text(""));
            worksheet.Save();
        }
        LocalizationProject actual = LocalizationProjectIO.Read(directory);
        Assert.Equal(JsonValueKind.Null, actual.Entries.Single(row => row.Key == "$en_scene_0000").ExtensionData["Field Name"].ValueKind);
        Assert.Equal("", actual.Entries.Single(row => row.Key == "$en_scene_0001").ExtensionData["Field Name"].GetString());
        string converted = Path.Combine(_root, "blank-field-names-json");
        LocalizationProjectIO.Convert(directory, converted, "json", "split", TestContext.Current.CancellationToken);
        LocalizationProject roundTrip = LocalizationProjectIO.Read(converted);
        Assert.Equal(JsonValueKind.Null, roundTrip.Entries.Single(row => row.Key == "$en_scene_0000").ExtensionData["Field Name"].ValueKind);
        Assert.Equal("", roundTrip.Entries.Single(row => row.Key == "$en_scene_0001").ExtensionData["Field Name"].GetString());
    }

    private static LocalizationProject Project(int schemaVersion)
    {
        return new LocalizationProject
        {
            Manifest = new LocalizationManifest
            {
                SchemaVersion = schemaVersion,
                TargetLanguage = "ru",
                ReferenceLanguages = new Dictionary<string, string> { ["Reference [jp]"] = "jp", ["Reference [en]"] = "en" }
            },
            Entries =
            [
                new LocalizationRecord
                {
                    Category = "Fields",
                    File = "scene",
                    Key = "$en_scene_0000",
                    Source = "synthetic base",
                    Target = "translation",
                    References = new Dictionary<string, string?> { ["Reference [jp]"] = "日本語", ["Reference [en]"] = "first\nsecond" },
                    ReferenceKeys = new Dictionary<string, IReadOnlyList<string>> { ["Reference [jp]"] = ["$jp_scene_0000"], ["Reference [en]"] = ["$en_scene_0000", "$en_scene_0001"] },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["Field Name"] = JsonSerializer.SerializeToElement("Synthetic room"),
                        ["Resource Hash"] = JsonSerializer.SerializeToElement("synthetic"),
                        ["Ordinal"] = JsonSerializer.SerializeToElement(0),
                        ["Details"] = JsonSerializer.SerializeToElement(new { present = true })
                    }
                }
            ]
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
