using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationAuthoringStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-authoring-" + Guid.NewGuid().ToString("N"));
    private static readonly LocalizationRowIdentity Identity = new("Fields", "scene", "$en_scene_0000");
    private static LocalizationProject Project() => new()
    {
        Manifest = new() { TargetLanguage = "ru", ReferenceLanguages = new Dictionary<string, string> { ["Reference [ja]"] = "ja", ["Reference [en]"] = "en" }, SourceReferenceLanguage = "ja" },
        Entries = [new() { Category = "Fields", File = "scene", Key = "$en_scene_0000", Source = "base", Target = "old", References = new Dictionary<string, string?> { ["Reference [ja]"] = "reference" }, ExtensionData = new Dictionary<string, JsonElement> { ["Unknown"] = JsonSerializer.SerializeToElement(new { keep = 7 }) } }, new() { Category = "Fields", File = "scene", Key = "$en_scene_0001", Source = "other", Target = "other" }]
    };

    [Fact]
    public async Task JsonRebasesUnrelatedRowsAndConflictsOnReferenceChanges()
    {
        LocalizationProjectIO.WriteNew(Project(), root, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        LocalizationRowSnapshot snapshot = await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken);
        string path = Path.Combine(root, "Localization/localization.json");
        JsonNode json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Fields"]!["scene"]!["$en_scene_0001"]!["Target"] = "external other";
        File.WriteAllText(path, json.ToJsonString());
        LocalizationCommitResult result = await store.CommitAsync(snapshot, new(true, "new"), TestContext.Current.CancellationToken);
        Assert.Equal(LocalizationCommitStatus.Saved, result.Status);
        LocalizationProject updated = LocalizationProjectIO.Read(root);
        Assert.Equal("external other", updated.Entries[1].Target);
        Assert.Equal(7, updated.Entries[0].ExtensionData["Unknown"].GetProperty("keep").GetInt32());
        snapshot = await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken);
        json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Fields"]!["scene"]!["$en_scene_0000"]!["Reference [ja]"] = "changed reference";
        File.WriteAllText(path, json.ToJsonString());
        Assert.Equal(LocalizationCommitStatus.Conflict, (await store.CommitAsync(snapshot, new(true, "ours"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("new", LocalizationProjectIO.Read(root).Entries[0].Target);
    }

    [Fact]
    public async Task WorkbookKeepsAncillarySheetFormulaAndFormatting()
    {
        LocalizationProjectIO.WriteNew(Project(), root, "xlsx", "single", TestContext.Current.CancellationToken);
        string path = Path.Combine(root, "Localization/localization.xlsx");
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, true))
        {
            WorkbookPart book = document.WorkbookPart!;
            WorksheetPart extra = book.AddNewPart<WorksheetPart>();
            extra.Worksheet = new Worksheet(new SheetData(new Row(new Cell { CellReference = "A1", CellFormula = new CellFormula("1+1"), CellValue = new CellValue("2") }) { RowIndex = 1 }));
            book.Workbook!.Sheets!.Append(new Sheet { Id = book.GetIdOfPart(extra), SheetId = 99, Name = "Notes" });
            book.Workbook.Save();
            extra.Worksheet.Save();
        }
        byte[] before;
        using (ZipArchive zip = ZipFile.OpenRead(path))
        {
            using Stream part = zip.Entries.Single(entry => entry.Name == "sheet2.xml").Open();
            using MemoryStream copy = new();
            part.CopyTo(copy);
            before = copy.ToArray();
        }
        LocalizationProjectStore store = new(root);
        LocalizationCommitResult result = await store.CommitAsync(await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken), new(true, "updated"), TestContext.Current.CancellationToken);
        Assert.Equal(LocalizationCommitStatus.Saved, result.Status);
        Assert.Equal("updated", LocalizationProjectIO.Read(root).Entries[0].Target);
        using ZipArchive after = ZipFile.OpenRead(path);
        using Stream preserved = after.Entries.Single(entry => entry.Name == "sheet2.xml").Open();
        using MemoryStream output = new();
        preserved.CopyTo(output);
        Assert.Equal(before, output.ToArray());
    }

    [Fact]
    public async Task NoOpAndCancellationDoNotRewriteAndBusyRetainsDraft()
    {
        LocalizationProjectIO.WriteNew(Project(), root, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        LocalizationRowSnapshot snapshot = await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken);
        string path = Path.Combine(root, "Localization/localization.json");
        byte[] before = File.ReadAllBytes(path);
        Assert.Equal(LocalizationCommitStatus.Unchanged, (await store.CommitAsync(snapshot, snapshot.Target, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(LocalizationCommitStatus.Cancelled, (await store.CommitAsync(snapshot, new(true, "cancel"), new(true))).Status);
        Assert.Equal(before, File.ReadAllBytes(path));
        LocalizationEditingSession session = new(store, new FieldMessageTargetSyntax(), snapshot);
        session.SetDraft(new(true, "draft"));
        session.Preview();
        using FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(LocalizationCommitStatus.Busy, (await session.SaveAsync(cancellation: TestContext.Current.CancellationToken)).Status);
        Assert.Equal("draft", session.Draft.Text);
        Assert.Equal(LocalizationSessionMode.Preview, session.Mode);
    }

    [Fact]
    public async Task ExistingSourceReferenceUpdatePreservesColumnOrderAndPrimaryDeclaration()
    {
        LocalizationProject project = Project();
        LocalizationProjectIO.WriteNew(project with { Manifest = project.Manifest with { ExtensionData = new() { ["sourceDatasets"] = JsonSerializer.SerializeToElement(new[] { new { Id = "ja", PrimaryReference = true }, new { Id = "en", PrimaryReference = false } }) } } }, root, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        LocalizationCommitResult result = await store.SetSourceReferenceAsync(await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken), "en", TestContext.Current.CancellationToken);
        Assert.Equal(LocalizationCommitStatus.Saved, result.Status);
        LocalizationProject loaded = LocalizationProjectIO.Read(root);
        Assert.Equal("en", LocalizationSourceRoles.Resolve(loaded.Manifest).Language);
        Assert.Equal(new[] { "ja", "en" }, loaded.Manifest.ReferenceLanguages.Values);
        Assert.Equal("old", loaded.Entries[0].Target);
        Assert.Throws<InvalidDataException>(() => LocalizationSourceRoles.Resolve(loaded.Manifest with { SourceReferenceLanguage = "unknown" }));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("xlsx")]
    public async Task CompleteTargetPreservesMissingNullAndEmptyAcrossExistingSaves(string format)
    {
        LocalizationProjectIO.WriteNew(Project(), root, format, "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        foreach (LocalizationTargetValue target in new LocalizationTargetValue[] { new(false, null), new(true, null), new(true, "") })
        {
            LocalizationCommitResult result = await store.CommitAsync(await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken), target, TestContext.Current.CancellationToken);
            Assert.Equal(LocalizationCommitStatus.Saved, result.Status);
            Assert.Equal(target, (await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken)).Target);
        }
    }

    [Fact]
    public async Task ConflictCandidatesRetainImmutableEvidenceAndReplayDoesNotAppendChanges()
    {
        LocalizationProjectIO.WriteNew(Project(), root, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        LocalizationRowSnapshot baseline = await store.ReadRowAsync(Identity, TestContext.Current.CancellationToken);
        LocalizationEditingSession session = new(store, new FieldMessageTargetSyntax(), baseline);
        session.SetDraft(new(true, "ours"));
        session.Preview();
        await store.CommitAsync(baseline, new(true, "external"), TestContext.Current.CancellationToken);
        Assert.Equal(LocalizationCommitStatus.Conflict, (await session.SaveAsync(cancellation: TestContext.Current.CancellationToken)).Status);
        LocalizationConflict captured = session.Conflict!;
        captured.Candidates[LocalizationConflictPane.Theirs] = new(true, "resolved exact");
        Assert.Equal("external", captured.Theirs.Target.Text);
        session.SelectConflict(LocalizationConflictPane.Theirs);
        await store.CommitAsync(captured.Theirs, new(true, "external again"), TestContext.Current.CancellationToken);
        Assert.Equal(LocalizationCommitStatus.Conflict, (await session.SaveAsync(cancellation: TestContext.Current.CancellationToken)).Status);
        Assert.Equal("resolved exact", session.Conflict!.Candidates[LocalizationConflictPane.Theirs].Text);
        string statePath = Path.GetTempFileName();
        try
        {
            session.Caret = 4;
            session.SelectionLength = 2;
            LocalizationAuthoringState state = new() { Drafts = [session.CaptureDraft("selected-package")] };
            await state.SaveAsync(statePath, TestContext.Current.CancellationToken);
            LocalizationRecoveredDraft recovered = Assert.Single((await LocalizationAuthoringState.LoadAsync(statePath, TestContext.Current.CancellationToken)).Drafts);
            session = LocalizationEditingSession.RestoreDraft(store, new FieldMessageTargetSyntax(), recovered);
            Assert.Equal("old", session.Conflict!.Base.Target.Text);
            Assert.Equal("ours", session.Conflict.Ours.Text);
            Assert.Equal("resolved exact", session.Conflict.Candidates[LocalizationConflictPane.Theirs].Text);
            Assert.Equal(4, session.Caret);
            Assert.Equal(2, session.SelectionLength);
        }
        finally
        {
            File.Delete(statePath);
        }
        session.SelectConflict(LocalizationConflictPane.Theirs);
        Assert.Equal(LocalizationCommitStatus.Saved, (await session.SaveAsync(cancellation: TestContext.Current.CancellationToken)).Status);
        Assert.Equal("external again", session.Changes.Single().Before.Target.Text);
        Assert.Equal("resolved exact", session.Changes.Single().After.Text);
        session.RecordChanges = false;
        session.SetDraft(new(true, "external"));
        session.Preview();
        await session.SaveAsync(cancellation: TestContext.Current.CancellationToken);
        Assert.Single(session.Changes);
    }

    [Fact]
    public async Task VerifiedMissingRowInsertsPayloadAndBindingSidecar()
    {
        LocalizationProject project = Project();
        LocalizationProjectIO.WriteNew(project with { Manifest = project.Manifest with { SchemaVersion = 2 } }, root, "json", "single", TestContext.Current.CancellationToken);
        LocalizationProjectStore store = new(root);
        LocalizationRecord verified = project.Entries[0] with { Key = "$en_scene_0002", Target = null };
        LocalizationRowSnapshot prepared = await store.ReadOrPrepareRowAsync(verified, TestContext.Current.CancellationToken);
        Assert.True(prepared.Missing);
        Assert.Equal(LocalizationCommitStatus.Saved, (await store.CommitAsync(prepared, new(true, "inserted"), TestContext.Current.CancellationToken)).Status);
        LocalizationProject loaded = LocalizationProjectIO.Read(root);
        Assert.Equal(3, loaded.Entries.Count);
        Assert.Equal("inserted", loaded.Entries.Single(row => row.Key == verified.Key).Target);
        Assert.Equal(7, loaded.Entries.Single(row => row.Key == verified.Key).ExtensionData["Unknown"].GetProperty("keep").GetInt32());
    }

    [Fact]
    public void InterruptedTransactionRollsBackBeforeReaderPublishes()
    {
        LocalizationProjectIO.WriteNew(Project(), root, "json", "single", TestContext.Current.CancellationToken);
        string relative = "Localization/localization.json";
        string backup = relative + ".authoring-test.bak";
        string candidate = relative + ".authoring-test.tmp";
        File.Copy(Path.Combine(root, relative), Path.Combine(root, backup));
        File.WriteAllText(Path.Combine(root, relative), "incomplete");
        File.WriteAllText(Path.Combine(root, ".localization-authoring.transaction.json"), JsonSerializer.Serialize(new[] { new { Destination = relative, Backup = backup, Candidate = candidate } }));
        Assert.Equal("old", LocalizationProjectIO.Read(root).Entries[0].Target);
        Assert.False(File.Exists(Path.Combine(root, ".localization-authoring.transaction.json")));
        Assert.False(File.Exists(Path.Combine(root, backup)));
    }

    [Fact]
    public void SourceRoleRecoveryRejectsContradictionAndKeepsColumnOrderIndependent()
    {
        LocalizationManifest manifest = Project().Manifest with { SourceReferenceLanguage = null, ExtensionData = new() { ["sourceDatasets"] = JsonSerializer.SerializeToElement(new[] { new { Id = "ja", PrimaryReference = true }, new { Id = "en", PrimaryReference = false } }) } };
        Assert.Equal("ja", LocalizationSourceRoles.Resolve(manifest).Language);
        Assert.True(LocalizationSourceRoles.Resolve(manifest).Inferred);
        Assert.Throws<InvalidDataException>(() => LocalizationSourceRoles.Resolve(manifest with { SourceReferenceLanguage = "en" }));
        Assert.Equal("ja", LocalizationSourceRoles.Resolve(manifest with { ReferenceLanguages = new Dictionary<string, string> { ["Reference [en]"] = "en", ["Reference [ja]"] = "ja" } }).Language);
        FieldMessageTargetSyntax syntax = new();
        Assert.Equal("{X10Y20W100H30F1L2B1C2}source", syntax.SetOrigin(new(false, null), "{X90Y80W999H999}source", "{W100H30F1L2B1C2}base", 10, 20).Text);
        Assert.Equal("{W010H30F1L2B1C2} body {Wait}", syntax.ResetOrigin(new(true, "{X-10Y20W010H30F1L2B1C2} body {Wait}")).Text);
        Assert.Equal("{X10Y20}body", syntax.SetOrigin(new(false, null), "{W999H999}body", 10, 20).Text);
        LocalizationTargetParts parts = syntax.Parse("{X-10Y20W010H30} body");
        Assert.Equal(new LocalizationTextSpan(1, 7), parts.PositionSpan);
        Assert.Equal("W010H30", "{X-10Y20W010H30} body".Substring(parts.SizeSpan!.Start, parts.SizeSpan.Length));
    }

    [Fact]
    public void IndependentReplayRetainsLaterOriginAndMaterializesDesignatedBody()
    {
        FieldMessageTargetSyntax syntax = new();
        LocalizationRowSnapshot baseline = new("project", Identity, Project().Entries[0], new(false, null), "revision");
        LocalizationTargetValue translated = new(true, "translation");
        LocalizationChange change = new(Guid.NewGuid(), baseline, translated, LocalizationReplay.Changes(syntax, baseline.Target, translated), "body");
        LocalizationTargetValue candidate = LocalizationReplay.Propose(syntax, change, new(true, "{X-2Y30W100H40}translation"), false, () => LocalizationSourceRoles.Seed(Project().Manifest, Project().Entries[0]).Text);
        Assert.Equal("{X-2Y30W100H40}reference", candidate.Text);
        Assert.Equal("{X5Y6W010H40} untouched {Wait}", syntax.SetOrigin(new(true, "{X-2Y30W010H40} untouched {Wait}"), "base", 5, 6).Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
