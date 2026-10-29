using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringControlTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    private const string PackageId = "Memoria/WorkbenchAcceptance";
    private const string SystemId = "ui.vectorpartycard.caption.lv";

    [Fact]
    public void CatalogControlAcceptsOnlyRegisteredCanonicalSystemIdentities()
    {
        string key = SystemTextCatalog.Key(SystemId);
        Assert.True(RecreateGame.TryResolveAuthoringCatalogKey(key, out LocalizationTextBinding? binding));
        Assert.Equal(key, binding!.Key);
        Assert.Contains("{0}", binding.Template);
        Assert.Empty(binding.Arguments);
        Assert.False(RecreateGame.TryResolveAuthoringCatalogKey("Lv 12", out _));
        Assert.False(RecreateGame.TryResolveAuthoringCatalogKey("$en_interface_system_unknown", out _));
        Assert.False(RecreateGame.TryResolveAuthoringCatalogKey("$en_field_0000", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualWorkbenchMenuConfirmationReachesItsDedicatedActionHandler(bool demo)
    {
        string? received = null;
        MenuController menus = new();
        menus.ConnectLocalizationWorkbench(() => [new("project", "Project", "", Action: new("localization-workbench", "select:fixture"))], action => received = action);
        menus.Open("localization-workbench", demo: demo);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("select:fixture", received);
    }

    [Fact]
    public void FixtureActionMessageCanBeReadRepeatedlyWithoutSelfReference()
    {
        MenuController menus = new();
        menus.Open("main", demo: true);
        menus.Execute(new("slot-select"));
        Assert.Contains("Fixture spell preview selected", menus.Message);
        Assert.Equal(menus.Message, menus.Message);
    }

    [Fact]
    public void LocalQueuePreservesCommittedUnicodeLanguageReleaseAndEditorCommands()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-authoring-protocol-" + Guid.NewGuid().ToString("N"));
        UiControlCommand? observed = null;
        ControlQueue queue = new(root, request =>
        {
            observed = request.Ui;
            return new { Accepted = true };
        });
        try
        {
            UiControlCommand ui = new(Edit: UiEditCommand.LineBreak, Text: "日本語😀{0}", Language: 2, Release: true, AuthoringKey: SystemTextCatalog.Key(SystemId), Shift: true);
            File.WriteAllText(Path.Combine(root, "requests", "one.json"), JsonSerializer.Serialize(new ControlRequest("one", "ui-input", Ui: ui), Json));
            queue.Poll();
            Assert.Equal(ui, observed);
            Assert.True(File.Exists(Path.Combine(root, "responses", "one.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstalledStandaloneFieldAndFormattedMenuUseRealPickerPreviewConflictAndPublication(bool native)
    {
        string game = TestResources.GameDirectory;
        string standalone = TestResources.FixturePath("App.Tests.LocalizationFieldQuestionTests.Standalone");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(standalone));
        GameSession initial = new(GameSession.Load(standalone));
        using GameWorld initialWorld = new(initial, game);
        initialWorld.EnsureField();
        Assert.NotNull(initialWorld.CurrentDialogue);
        Assert.True(initialWorld.CurrentDialogue!.MessageId >= 0, "An ordinary field message, rather than a generated common message, is required.");
        using var original = GameDataSourceFactory.Open(game);
        Assert.SkipUnless(original.Descriptor.Language.Equals("en", StringComparison.OrdinalIgnoreCase), "This optional standalone uses the English script-base contract.");
        string fieldKey = FieldLocalizationKey.Create("en", initialWorld.Field!.Name, initialWorld.CurrentDialogue.MessageId);
        LocalizationSourceCatalog source = LocalizationSourceExporter.Read(game, "en", TestContext.Current.CancellationToken);
        LocalizationSourceEntry fieldSource = Assert.Single(source.Entries, row => row.Key == fieldKey);
        LocalizationSourceEntry systemSource = Assert.Single(SystemTextCatalog.SourceEntries(), row => row.Key == SystemTextCatalog.Key(SystemId));
        LocalizationManifest manifest = new()
        {
            TargetLanguage = "workbench-acceptance",
            SourceLanguage = "en",
            FieldScriptBaseLanguage = "en",
            SourceReferenceLanguage = "en",
            ReferenceLanguages = new Dictionary<string, string> { ["Reference [en]"] = "en" },
            MappingVersion = FieldCorrespondenceCatalog.Version
        };
        LocalizationProject project = new() { Manifest = manifest, Entries = [LocalizationProjectBuilder.CreateVerifiedRecord(fieldSource, manifest), LocalizationProjectBuilder.CreateVerifiedRecord(systemSource, manifest)] };
        string root = Path.Combine(Path.GetTempPath(), "memoria-workbench-host-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string package = Path.Combine(mods, "Memoria", "WorkbenchAcceptance");
        LocalizationProjectIO.WriteNew(project, package, "json", "single", TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nname=Workbench acceptance\nenabled=0\n");
        string settings = Path.Combine(root, "settings");
        Directory.CreateDirectory(settings);
        new UiPreferences { MusicVolume = 0, SoundVolume = 0, VoiceVolume = 0, MenuPresentation = MenuPresentation.Vector, TextFontMode = native ? GameTextFontMode.Native : GameTextFontMode.System, TextLayoutMode = native ? GameTextLayoutMode.OriginalCompatible : GameTextLayoutMode.Adaptive }.Save(Path.Combine(settings, "ui-settings.json"));
        string control = Path.Combine(root, "control");
        Directory.CreateDirectory(Path.Combine(control, "requests"));
        ProcessStartInfo start = new(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Memoria.FF8.exe" : "Memoria.FF8")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--state", standalone, "--game-dir", game, "--mods-dir", mods, "--settings-dir", settings, "--control-dir", control, "--control-step", "--resolution", "1024x768", "--exit-after", "120" })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        int sequence = 0;
        try
        {
            await WaitUi(state => state.GetProperty("Localization").ValueKind == JsonValueKind.Object);
            await Request("ui-open", new(Screen: "localization-workbench"));
            await SelectEntry("authoring-project-" + PackageId);
            await WaitUi(state => state.GetProperty("Localization").GetProperty("SelectedPackage").GetString() == PackageId && !state.GetProperty("Localization").GetProperty("LoadingProject").GetBoolean());
            await SelectEntry("authoring-toggle");
            JsonElement ready = await WaitLocalization(state => state.GetProperty("Enabled").GetBoolean() && Hits(state).Any(hit => hit.GetProperty("Key").GetString() == fieldKey));
            Assert.Equal(native ? "Native" : "System", ready.GetProperty("Native").GetProperty("FontMode").GetString());
            Assert.Equal(native ? "OriginalCompatible" : "Adaptive", ready.GetProperty("Native").GetProperty("LayoutMode").GetString());
            if (native)
            {
                Assert.True(ready.GetProperty("Native").GetProperty("Ready").GetBoolean(), ready.GetProperty("Native").GetProperty("Diagnostic").GetString());
            }
            JsonElement simulation = ready.GetProperty("Simulation").Clone();
            await Pick(fieldKey);
            JsonElement opened = await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Edit" && state.GetProperty("Identity").GetProperty("Key").GetString() == fieldKey);
            Assert.False(opened.GetProperty("Dirty").GetBoolean());
            Assert.True(string.IsNullOrEmpty(opened.GetProperty("Draft").GetProperty("Text").GetString()));
            FieldMessageTargetSyntax syntax = new();
            LocalizationTargetParts parts = syntax.Parse(fieldSource.Text);
            string translation = syntax.Compose(parts with { Body = (native ? "Native " : "日本語 😀 ") + parts.Body });
            await Request("ui-input", new(Edit: UiEditCommand.SelectAll));
            await Request("ui-input", new(Language: 1));
            Assert.True((await Request("ui-status")).GetProperty("Localization").GetProperty("ReferenceReadOnly").GetBoolean());
            await Request("ui-input", new(Text: translation));
            await Preview();
            Assert.True(string.IsNullOrEmpty(LocalizationProjectIO.Read(package).Entries.Single(row => row.Key == fieldKey).Target));
            await Request("ui-input", new(Edit: UiEditCommand.Confirm));
            Assert.Equal("Preview", (await Request("ui-status")).GetProperty("Localization").GetProperty("Mode").GetString());
            await Save();
            Assert.Equal(translation, LocalizationProjectIO.Read(package).Entries.Single(row => row.Key == fieldKey).Target);
            Assert.True(JsonElement.DeepEquals(simulation, (await Request("ui-status")).GetProperty("Localization").GetProperty("Simulation")), "Authoring changed native field/choice state.");

            string ours = syntax.Compose(parts with { Body = "Ours " + parts.Body });
            string theirs = syntax.Compose(parts with { Body = "External " + parts.Body });
            string merged = syntax.Compose(parts with { Body = (native ? "Merged " : "Merged 日本語 ") + parts.Body });
            await Replace(ours);
            await Preview();
            LocalizationProjectStore external = new(package);
            LocalizationRowIdentity identity = new(fieldSource.Category, fieldSource.File, fieldKey);
            LocalizationRowSnapshot outside = await external.ReadRowAsync(identity, timeout.Token);
            Assert.Equal(LocalizationCommitStatus.Saved, (await external.CommitAsync(outside, new(true, theirs), timeout.Token)).Status);
            await Request("ui-input", new(Release: true));
            await Request("ui-input", new(Edit: UiEditCommand.Confirm));
            await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Conflict");
            await ClickControl("conflict.Ours");
            await Replace(merged);
            await ClickControl("conflict.use");
            await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Preview");
            Assert.Equal(theirs, LocalizationProjectIO.Read(package).Entries.Single(row => row.Key == fieldKey).Target);
            await Save();
            Assert.Equal(merged, LocalizationProjectIO.Read(package).Entries.Single(row => row.Key == fieldKey).Target);
            await Request("ui-input", new(Release: true, Edit: UiEditCommand.Cancel));

            await Request("ui-open", new(Screen: "main", Demo: true));
            string systemKey = SystemTextCatalog.Key(SystemId);
            JsonElement menu = await WaitLocalization(state => Hits(state).Any(hit => hit.GetProperty("Key").GetString() == systemKey && hit.GetProperty("Arguments").GetArrayLength() > 0));
            JsonElement formatted = Hits(menu).Last(hit => hit.GetProperty("Key").GetString() == systemKey);
            Assert.Contains("{0}", formatted.GetProperty("Template").GetString());
            Assert.NotEqual(formatted.GetProperty("Template").GetString(), formatted.GetProperty("Text").GetString());
            await Pick(systemKey);
            JsonElement templateEditor = await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Edit" && state.GetProperty("Identity").GetProperty("Key").GetString() == systemKey);
            Assert.Contains("{0}", templateEditor.GetProperty("EditorText").GetString());
            await Replace("Niveau {0}");
            await Preview();
            await Save();
            Assert.Equal("Niveau {0}", LocalizationProjectIO.Read(package).Entries.Single(row => row.Key == systemKey).Target);
            await Request("ui-input", new(Release: true, Edit: UiEditCommand.Cancel));
            await WaitLocalization(state => Hits(state).Any(hit => hit.GetProperty("Key").GetString() == systemKey && hit.GetProperty("Text").GetString()!.StartsWith("Niveau ", StringComparison.Ordinal)));
            string capture = Path.Combine(root, "workbench.png");
            await Request("capture", path: capture);
            while (!File.Exists(capture))
            {
                await Task.Delay(20, timeout.Token);
            }
            Assert.True(File.Exists(capture));
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(standalone)));
            Assert.Contains("enabled=0", File.ReadAllText(Path.Combine(package, "mod.ini")));
            PreserveEvidence(capture, new { Presentation = native ? "NativeOriginal" : "SystemAdaptive", FieldKey = fieldKey, SystemKey = systemKey, OriginalSaveUnchanged = true, NativeSimulationUnchanged = true, PackageStayedDisabled = true });
            await Request("quit");
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            output.WriteLine("Actual host: field picking, reference input handoff, Preview/Save release gate, external conflict editing, formatted template picking and selected-mod publication passed.");

            async Task<JsonElement> Request(string command, UiControlCommand? ui = null, string? path = null)
            {
                string id = (++sequence).ToString("D5");
                string request = Path.Combine(control, "requests", id + ".json");
                string response = Path.Combine(control, "responses", id + ".json");
                File.WriteAllText(request + ".tmp", JsonSerializer.Serialize(new ControlRequest(id, command, Ui: ui, Path: path), Json));
                File.Move(request + ".tmp", request);
                while (!File.Exists(response) && !process.HasExited)
                {
                    await Task.Delay(15, timeout.Token);
                }
                Assert.True(File.Exists(response), "Host exited before control response " + command + ". " + (process.HasExited ? await stderr : ""));
                using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(response));
                Assert.True(receipt.RootElement.GetProperty("Ok").GetBoolean(), receipt.RootElement.GetRawText());
                return receipt.RootElement.GetProperty("Result").Clone();
            }
            async Task<JsonElement> WaitUi(Func<JsonElement, bool> predicate)
            {
                while (true)
                {
                    JsonElement state = await Request("ui-status");
                    if (predicate(state))
                    {
                        return state;
                    }
                    await Task.Delay(20, timeout.Token);
                }
            }
            async Task<JsonElement> WaitLocalization(Func<JsonElement, bool> predicate) => (await WaitUi(state => predicate(state.GetProperty("Localization")))).GetProperty("Localization").Clone();
            async Task SelectEntry(string id)
            {
                JsonElement state = await Request("ui-status");
                JsonElement[] entries = state.GetProperty("Entries").EnumerateArray().ToArray();
                int target = Array.FindIndex(entries, entry => entry.GetProperty("Id").GetString() == id);
                Assert.True(target >= 0, "Missing menu entry " + id);
                for (int attempt = 0; state.GetProperty("SelectedIndex").GetInt32() != target && attempt < entries.Length * 2; attempt++)
                {
                    state = await Request("ui-input", new(Input: MenuInput.Down));
                }
                Assert.Equal(target, state.GetProperty("SelectedIndex").GetInt32());
                await Request("ui-input", new(Input: MenuInput.Confirm));
            }
            async Task Pick(string key)
            {
                JsonElement state = await WaitLocalization(value => Hits(value).Any(hit => hit.GetProperty("Key").GetString() == key));
                JsonElement hit = Hits(state).Last(value => value.GetProperty("Key").GetString() == key);
                await Click(hit.GetProperty("Bounds"));
                JsonElement picked = await WaitLocalization(value => value.GetProperty("LoadingRow").GetBoolean() || value.GetProperty("Mode").GetString() == "Edit" || value.GetProperty("Candidates").ValueKind == JsonValueKind.Array);
                if (picked.GetProperty("Candidates").ValueKind == JsonValueKind.Array)
                {
                    JsonElement[] candidates = picked.GetProperty("Candidates").EnumerateArray().ToArray();
                    int candidate = Array.FindIndex(candidates, value => value.GetProperty("Key").GetString() == key);
                    Assert.True(candidate >= 0, "The rendered point did not offer the expected canonical binding.");
                    for (int index = 0; index < candidate; index++)
                    {
                        await Request("ui-input", new(Edit: UiEditCommand.Down));
                    }
                    await Request("ui-input", new(Release: true, Edit: UiEditCommand.Confirm));
                }
            }
            async Task ClickControl(string id)
            {
                JsonElement state = await WaitLocalization(value => value.GetProperty("Controls").EnumerateArray().Any(hit => hit.GetProperty("Id").GetString() == id));
                await Click(state.GetProperty("Controls").EnumerateArray().Last(hit => hit.GetProperty("Id").GetString() == id).GetProperty("Bounds"));
            }
            async Task Click(JsonElement bounds)
            {
                float x = bounds.GetProperty("X").GetSingle() + bounds.GetProperty("Width").GetSingle() / 2;
                float y = bounds.GetProperty("Y").GetSingle() + bounds.GetProperty("Height").GetSingle() / 2;
                await Request("ui-input", new(X: x, Y: y, Pressed: true));
                await Request("ui-input", new(X: x, Y: y, Pressed: false));
            }
            async Task Replace(string text)
            {
                await Request("ui-input", new(Edit: UiEditCommand.SelectAll));
                await Request("ui-input", new(Text: text));
            }
            async Task Preview()
            {
                JsonElement state = (await Request("ui-status")).GetProperty("Localization");
                if (state.GetProperty("Completions").GetArrayLength() != 0)
                {
                    await Request("ui-input", new(Release: true, Edit: UiEditCommand.Cancel));
                }
                await Request("ui-input", new(Release: true));
                await Request("ui-input", new(Edit: UiEditCommand.Confirm));
                await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Preview");
            }
            async Task Save()
            {
                await Request("ui-input", new(Release: true));
                await Request("ui-input", new(Edit: UiEditCommand.Confirm));
                await WaitLocalization(state => state.GetProperty("Mode").GetString() == "Edit" && !state.GetProperty("Dirty").GetBoolean() && !state.GetProperty("LoadingProject").GetBoolean());
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            string diagnostic = await stderr;
            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                output.WriteLine(diagnostic.Length > 4000 ? diagnostic[^4000..] : diagnostic);
            }
            await stdout;
            Directory.Delete(root, recursive: true);
        }
    }
    private static IEnumerable<JsonElement> Hits(JsonElement state) => state.GetProperty("VisibleHits").EnumerateArray();
    private static void PreserveEvidence(string capture, object summary)
    {
        string? configured = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_WORKBENCH_OUTPUT");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }
        string destination = Path.Combine(configured, "host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        File.Copy(capture, Path.Combine(destination, "workbench.png"));
        File.WriteAllText(Path.Combine(destination, "validation.json"), JsonSerializer.Serialize(summary));
    }
}
