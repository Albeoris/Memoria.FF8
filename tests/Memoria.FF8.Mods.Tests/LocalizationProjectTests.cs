using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "memoria-localization-tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("single", "split")]
    [InlineData("split", "single")]
    public void UnicodePresenceAndTypedExtensionsRoundTripThroughWorkbook(string firstLayout, string secondLayout)
    {
        LocalizationProject project = Project();
        string json = Path.Combine(_root, "json");
        string workbook = Path.Combine(_root, "xlsx");
        string final = Path.Combine(_root, "final");
        LocalizationProjectIO.WriteNew(project, json, "json", firstLayout, TestContext.Current.CancellationToken);
        string output = File.ReadAllText(Path.Combine(json, "Localization", firstLayout == "single" ? "localization.json" : "Fields/bgroad_1.json"));
        Assert.Contains("Японский 日本語　😀\uE123", output);
        Assert.Contains("\\\\u3000", output);
        LocalizationProjectIO.Convert(json, workbook, "xlsx", secondLayout, TestContext.Current.CancellationToken);
        LocalizationProjectIO.Convert(workbook, final, "json", firstLayout, TestContext.Current.CancellationToken);
        LocalizationProject actual = LocalizationProjectIO.Read(final);
        Assert.Equal(project.Manifest.ProjectId, actual.Manifest.ProjectId);
        Assert.Equal(project.Manifest.TargetLanguage, actual.Manifest.TargetLanguage);
        Assert.Equal(project.Entries[0].Source, actual.Entries[0].Source);
        Assert.Equal(project.Entries[0].Target, actual.Entries[0].Target);
        Assert.True(JsonElement.DeepEquals(project.Entries[0].ExtensionData["Object"], actual.Entries[0].ExtensionData["Object"]));
        Assert.True(actual.Entries[1].SourcePresent);
        Assert.Equal("", actual.Entries[1].Source);
        Assert.False(actual.Entries[2].SourcePresent);
        Assert.Null(actual.Entries[2].Source);
        Assert.False(actual.Entries[2].ReferencePresence["Reference [en]"]);
        Assert.False(actual.Entries[2].ExtensionData.ContainsKey("Comment"));
        Assert.Contains("enabled=0", File.ReadAllText(Path.Combine(final, "mod.ini")));
    }

    [Fact]
    public void OpenSharedWorkbookCanBeReadAndFormulaIsRejected()
    {
        string directory = Path.Combine(_root, "xlsx");
        LocalizationProjectIO.WriteNew(Project(), directory, "xlsx", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "Localization/localization.xlsx");
        using (FileStream editor = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal(3, LocalizationProjectIO.Read(directory).Entries.Count);
        }
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, true))
        {
            WorksheetPart worksheet = document.WorkbookPart!.WorksheetParts.Single();
            Cell target = worksheet.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "D2");
            target.CellFormula = new CellFormula("1+1");
            worksheet.Worksheet.Save();
        }
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory));
        Assert.Contains("Formula", error.Message);
        Assert.Contains("D2", error.Message);
    }

    [Fact]
    public void RejectsDuplicatesTraversalMissingSidecarAndExistingDestination()
    {
        string directory = Path.Combine(_root, "json");
        LocalizationProjectIO.WriteNew(Project(), directory, "json", "single", TestContext.Current.CancellationToken);
        Assert.Throws<IOException>(() => LocalizationProjectIO.WriteNew(Project(), directory, "json", "single", TestContext.Current.CancellationToken));
        string payload = Path.Combine(directory, "Localization/localization.json");
        File.WriteAllText(payload, "{\"Fields\":{\"bgroad_1\":{\"$en_bgroad_1_0000\":{\"Target\":\"one\",\"Target\":\"two\"}}}}");
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory));
        string xlsx = Path.Combine(_root, "xlsx");
        LocalizationProjectIO.WriteNew(Project(), xlsx, "xlsx", "single", TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(xlsx, "Localization/localization.extensions.json"));
        Assert.Throws<FileNotFoundException>(() => LocalizationProjectIO.Read(xlsx));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(Project() with { Entries = [Project().Entries[0] with { File = "../bad" }] }));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(Project() with { Entries = [Project().Entries[0], Project().Entries[0]] }));
    }

    [Fact]
    public void CancellationAndExcelLimitsDoNotPublishHalfWrittenPackage()
    {
        string cancelled = Path.Combine(_root, "cancelled");
        Assert.Throws<OperationCanceledException>(() => LocalizationProjectIO.WriteNew(Project(), cancelled, "json", "single", new CancellationToken(true)));
        Assert.False(Directory.Exists(cancelled));
        string tooLarge = Path.Combine(_root, "too-large");
        LocalizationProject project = Project();
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.WriteNew(project with { Entries = [project.Entries[0] with { Target = new string('a', 32768) }] }, tooLarge, "xlsx", "single", TestContext.Current.CancellationToken));
        Assert.Contains("$en_bgroad_1_0000", error.Message);
        Assert.False(Directory.Exists(tooLarge));
    }

    [Fact]
    public void RestrictiveHandleProducesSharingDiagnosticAndCanRetryAfterRelease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string directory = Path.Combine(_root, "locked");
        LocalizationProjectIO.WriteNew(Project(), directory, "xlsx", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "Localization/localization.xlsx");
        using (FileStream owner = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            IOException error = Assert.ThrowsAny<IOException>(() => LocalizationProjectIO.Read(directory));
            LocalizationFileAccessDiagnostic diagnostic = LocalizationFileAccessDiagnostic.FromException(path, error);
            Assert.True(diagnostic.IsSharingViolation);
            Assert.Contains("Windows denies read access", diagnostic.Message);
            Assert.NotNull(diagnostic.Applications);
        }
        Assert.Equal(3, LocalizationProjectIO.Read(directory).Entries.Count);
    }

    [Fact]
    public void SingleJsonDocumentCanExceedTheArtworkByteLimitWithoutLosingRows()
    {
        LocalizationProject project = Project();
        string directory = Path.Combine(_root, "large-document");
        LocalizationProjectIO.WriteNew(project, directory, "json", "single", TestContext.Current.CancellationToken);
        string payload = Path.Combine(directory, "Localization/localization.json");
        byte[] whitespace = new byte[1024 * 1024];
        Array.Fill(whitespace, (byte)' ');
        using (FileStream stream = new(payload, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            while (stream.Length < 129L * 1024 * 1024)
            {
                TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                stream.Write(whitespace);
            }
        }
        LocalizationProject actual = LocalizationProjectIO.Read(directory);
        Assert.Equal(project.Manifest.ProjectId, actual.Manifest.ProjectId);
        Assert.Equal(project.Entries.Count, actual.Entries.Count);
        Assert.Equal(project.Entries.Select(row => row.Key), actual.Entries.Select(row => row.Key));
        Assert.Equal(project.Entries.Select(row => row.Target), actual.Entries.Select(row => row.Target));
        Assert.True(JsonElement.DeepEquals(project.Entries[0].ExtensionData["Object"], actual.Entries[0].ExtensionData["Object"]));
    }

    private static LocalizationProject Project()
    {
        Dictionary<string, string> references = new() { ["Reference [en]"] = "en" };
        return new LocalizationProject
        {
            Manifest = new LocalizationManifest { TargetLanguage = "ru (Literary [edition])", ReferenceLanguages = references },
            Entries =
            [
                new LocalizationRecord { Category = "Fields", File = "bgroad_1", Key = "$en_bgroad_1_0000", Source = "Японский 日本語　😀\uE123 literal \\u3000\r\n_x0041_", Target = "=+@- translation", References = new Dictionary<string, string?> { ["Reference [en]"] = "" }, ExtensionData = new Dictionary<string, JsonElement> { ["Comment"] = JsonSerializer.SerializeToElement("note"), ["Object"] = JsonSerializer.SerializeToElement(new { nested = new[] { 1, 2 } }) } },
                new LocalizationRecord { Category = "Fields", File = "bgroad_1", Key = "$en_bgroad_1_0001", Source = "", Target = "  " },
                new LocalizationRecord { Category = "Fields", File = "bgroad_1", Key = "$ja_bgroad_1_0002", SourcePresent = false }
            ]
        };
    }

    [Fact]
    public void EffectiveExportUsesOnlyWinningTranslationsAndReconciliationRetainsWork()
    {
        LocalizationProject original = Project();
        LocalizationProject patch = original with { Manifest = original.Manifest with { ProjectId = "patch" }, Entries = [original.Entries[0] with { Target = "" }, original.Entries[1] with { Target = "patch" }] };
        LocalizationProject flattened = LocalizationProjectExport.Resolve([original, patch], "ru (Flattened)", 42);
        Assert.NotEqual(original.Manifest.ProjectId, flattened.Manifest.ProjectId);
        Assert.Equal(original.Entries[0].Target, flattened.Entries[0].Target);
        Assert.Equal("patch", flattened.Entries[1].Target);
        Assert.Null(flattened.Entries[2].Target);
        Assert.Equal(42, flattened.Manifest.ExtensionData["sourceGeneration"].GetInt64());
        Assert.Throws<InvalidDataException>(() => LocalizationProjectExport.Resolve([original, patch], original.Manifest.TargetLanguage));
        LocalizationProject fresh = original with { Entries = [original.Entries[0] with { Source = "updated", Target = null }, original.Entries[1] with { Key = "$en_bgroad_1_0003", Target = null }] };
        LocalizationProjectReconciliation reconciled = LocalizationProjectExport.Reconcile(original, fresh);
        Assert.Equal(original.Manifest.ProjectId, reconciled.Project.Manifest.ProjectId);
        Assert.Equal(original.Entries[0].Target, reconciled.Project.Entries[0].Target);
        Assert.Equal(["$en_bgroad_1_0000"], reconciled.ChangedSourceKeys);
        Assert.Equal(["$en_bgroad_1_0003"], reconciled.NewKeys);
        Assert.Equal(2, reconciled.ObsoleteKeys.Count);
        Assert.Equal(4, reconciled.Project.Entries.Count);
    }

    [Fact]
    public void KnownControlsAndRequiredDynamicPlaceholdersValidateWithoutEuropeanEncoding()
    {
        LocalizationRecord row = Project().Entries[0] with { Source = "{Var 2} {Char Squall}", Target = "日本語 {Char Squall} {Var 2} {Custom style}" };
        LocalizationTextValidation.Validate(row);
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "{Char Squall}" }));
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "{Var 999} {Char Squall}" }));
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "{Var 2} {Char Squall}\0" }));
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "{Var 2" }));
    }

    [Fact]
    public void SharedRichStringsSparseCellsAndReorderedColumnsPreserveTypedAuthoringValues()
    {
        string directory = Path.Combine(_root, "shared");
        LocalizationProjectIO.WriteNew(Project(), directory, "xlsx", "single", TestContext.Current.CancellationToken);
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(Path.Combine(directory, "Localization/localization.xlsx"), true))
        {
            WorkbookPart workbook = document.WorkbookPart!;
            SharedStringTablePart shared = workbook.AddNewPart<SharedStringTablePart>();
            shared.SharedStringTable = new SharedStringTable(new SharedStringItem(new Run(new Text("Shared ")), new Run(new Text("日本語"))));
            Worksheet worksheet = workbook.WorksheetParts.Single().Worksheet!;
            Cell source = worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "C2");
            source.RemoveAllChildren();
            source.DataType = CellValues.SharedString;
            source.CellValue = new CellValue("0");
            worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "D2").Remove();
            foreach (Cell cell in worksheet.Descendants<Cell>())
            {
                string address = cell.CellReference!.Value!;
                if (address.StartsWith('C'))
                {
                    cell.CellReference = "D" + address[1..];
                }
                else if (address.StartsWith('D'))
                {
                    cell.CellReference = "C" + address[1..];
                }
            }
            Row[] rows = worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
            rows[0].Append(new Cell(new InlineString(new Text("Score"))) { CellReference = "G1", DataType = CellValues.InlineString });
            rows[1].Append(new Cell(new CellValue("12.5")) { CellReference = "G2", DataType = CellValues.Number });
            shared.SharedStringTable.Save();
            worksheet.Save();
        }
        LocalizationRecord actual = LocalizationProjectIO.Read(directory).Entries[0];
        Assert.Equal("Shared 日本語", actual.Source);
        Assert.True(string.IsNullOrEmpty(actual.Target));
        Assert.Equal(JsonValueKind.Number, actual.ExtensionData["Score"].ValueKind);
        Assert.Equal(12.5m, actual.ExtensionData["Score"].GetDecimal());
    }

    [Theory]
    [InlineData("projectId")]
    [InlineData("schemaVersion")]
    public void MissingSerializedManifestIdentityOrSchemaDoesNotUseCreationDefaults(string member)
    {
        string directory = Path.Combine(_root, "incomplete");
        LocalizationProjectIO.WriteNew(Project(), directory, "json", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, LocalizationProjectIO.ManifestRelativePath);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        manifest.Remove(member);
        File.WriteAllText(path, manifest.ToJsonString());
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory));
        Assert.Contains(member, error.Message);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("split")]
    public void RuntimeRemovalModeClearsOnlyRemovedPayloadOwnershipAndRequiresProjectMetadata(string layout)
    {
        string directory = Path.Combine(_root, "removed");
        LocalizationProject project = Project();
        LocalizationRecord retained = project.Entries[0] with { Category = "Magic", File = "names_magic", Key = "$en_names_magic_0000", Target = "Retained" };
        LocalizationProjectIO.WriteNew(project with { Entries = [.. project.Entries, retained] }, directory, "xlsx", layout, TestContext.Current.CancellationToken);
        string payload = Path.Combine(directory, "Localization", layout == "single" ? "localization.xlsx" : "Fields/bgroad_1.xlsx");
        File.Delete(payload);
        Assert.Throws<FileNotFoundException>(() => LocalizationProjectIO.Read(directory));
        LocalizationProject runtime = LocalizationProjectIO.Read(directory, allowMissingPayloads: true);
        if (layout == "single")
        {
            Assert.Empty(runtime.Entries);
        }
        else
        {
            LocalizationRecord row = Assert.Single(runtime.Entries);
            Assert.Equal("Retained", row.Target);
            Assert.True(JsonElement.DeepEquals(retained.ExtensionData["Object"], row.ExtensionData["Object"]));
            string sidecar = Path.Combine(directory, "Localization/localization.extensions.json");
            JsonObject metadata = JsonNode.Parse(File.ReadAllText(sidecar))!.AsObject();
            metadata.Add("Magic/rogue/$en_rogue_0000", metadata.First().Value!.DeepClone());
            File.WriteAllText(sidecar, metadata.ToJsonString());
            Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(directory, allowMissingPayloads: true));
        }
        File.Delete(Path.Combine(directory, "Localization/localization.extensions.json"));
        Assert.Throws<FileNotFoundException>(() => LocalizationProjectIO.Read(directory, allowMissingPayloads: true));
        string manifest = Path.Combine(directory, LocalizationProjectIO.ManifestRelativePath);
        File.Delete(manifest);
        FileNotFoundException error = Assert.Throws<FileNotFoundException>(() => LocalizationProjectIO.Read(directory, allowMissingPayloads: true));
        Assert.Equal(Path.GetFullPath(manifest), error.FileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
