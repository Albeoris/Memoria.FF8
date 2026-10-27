using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Assets;
using System.Globalization;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SystemLocalizationPresentationTests
{
    [Fact]
    public void ModelAssetSearchReadsTranslatedPresentationAndKeepsNativeSelectionKeys()
    {
        ModelBrowserController browser = new([new("model-id", "Native model", "native-category", "", [new("original", "Original")])], () => { }, (_, _) => { }, (_, _) => { });
        string name = "翻訳された名前";
        ModelBrowserAssetRow asset = new("native-asset", "Original identifier", "native-group", false) { ReadName = () => name, ReadGroup = () => "表示グループ" };
        browser.AddAsset(asset);
        browser.OpenAssetPanel();
        browser.SetAssetSearch("翻訳");
        Assert.Equal("native-asset", Assert.Single(browser.VisibleAssets).Id);
        browser.ToggleAsset("native-asset");
        Assert.DoesNotContain("native-asset", browser.SelectedAssets);
        name = "Переведённое имя";
        browser.SetAssetSearch("Переведённое");
        Assert.Equal("native-asset", Assert.Single(browser.VisibleAssets).Id);
        Assert.Equal("Original identifier", asset.Name);
        Assert.Equal("native-group", asset.Group);
    }

    [Fact]
    public void NonEnglishBaseKeepsItsAbsenceAndUsesOrdinaryEnglishReference()
    {
        LocalizationSourceEntry native = new("Fields", "synthetic", "$fr_synthetic_0000", "French field sample", "fr", new Dictionary<string, string?>());
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(new("fr", "fr", "target", ["en"], "Test package"), new Dictionary<string, LocalizationSourceCatalog> { ["fr"] = new([native], [], []), ["en"] = new([], [], []) }, cancellation: TestContext.Current.CancellationToken, includeSystem: true);
        LocalizationRecord system = Assert.Single(project.Entries, row => row.Key == "$fr_" + SystemTextCatalog.File + "_common.cancel");
        Assert.False(system.SourcePresent);
        Assert.Null(system.Source);
        Assert.Equal("en", project.Manifest.ReferenceLanguages["Reference [en]"]);
        Assert.Equal("Cancel", system.References["Reference [en]"]);
        Assert.True(system.ReferencePresence["Reference [en]"]);
        Assert.Equal(new[] { SystemTextCatalog.Key("common.cancel") }, system.ReferenceKeys["Reference [en]"]);
        Assert.False(system.ReferencePresence["Reference [fr]"]);
        Assert.Null(system.References["Reference [fr]"]);
        Assert.DoesNotContain(project.Entries, row => row.Category == "System" && row.Key.StartsWith("$en_", StringComparison.Ordinal));
        LocalizationRecord field = Assert.Single(project.Entries, row => row.Category == "Fields");
        Assert.Equal("French field sample", field.Source);
        Assert.False(field.ReferencePresence["Reference [en]"]);
        Assert.Null(field.References["Reference [en]"]);
        LocalizationProjectIO.Validate(project);
        LocalizationProject english = LocalizationProjectBuilder.CreateProject(new("en", "en", "target", [], "Test package"), new Dictionary<string, LocalizationSourceCatalog> { ["en"] = new([], [], []) }, cancellation: TestContext.Current.CancellationToken, includeSystem: true);
        LocalizationRecord englishSystem = Assert.Single(english.Entries, row => row.Key == SystemTextCatalog.Key("common.cancel"));
        Assert.True(englishSystem.SourcePresent);
        Assert.Equal("Cancel", englishSystem.Source);
    }

    [Fact]
    public void ControlsUseEnumIdentityAndExportItsAuthoredDefault()
    {
        ControlsProfile controls = ControlsProfile.CreateDefault();
        MenuController menu = new(() => new MenuData());
        menu.ConnectControls(() => controls, _ => Assert.Fail("Reading translated bindings must not write the profile."));
        string translated = "Вверх";
        menu.ConnectLocalizationText((key, original) => key == SystemTextCatalog.Key("controls.action.MoveUp") ? translated : original);
        menu.Open("config-keyboard");
        MenuEntry entry = Assert.Single(menu.Entries, row => row.Action?.SubjectId == "MoveUp");
        Assert.Equal("Вверх", entry.Label);
        Assert.Equal("Keyboard", entry.Action!.Value);
        translated = "上へ";
        Assert.Equal("上へ", Assert.Single(menu.Entries, row => row.Action?.SubjectId == "MoveUp").Label);
        Assert.Equal("Move Up", SystemTextCatalog.Defaults["controls.action.MoveUp"]);
    }

    [Fact]
    public void LimitPresentationUsesTypedStateAndPreservesActionDomainValues()
    {
        MenuData data = new();
        MenuCharacter character = new() { Id = "character-0", Name = "Chosen name", NativeLimitCommand = 5 };
        character.LimitMoves.Add(new() { Id = "move", Name = "Owned move", Description = "Owned description", Learned = true });
        character.LimitSettings["Auto"] = "On";
        character.LimitSettings["Indicator"] = "Off";
        character.LimitToggles["Auto"] = true;
        character.LimitToggles["Indicator"] = false;
        data.Characters.Add(character);
        MenuController menu = new(() => data);
        menu.ConnectLocalizationText((key, original) => key == SystemTextCatalog.Key("limit.setting.auto") ? "Авто" : key == SystemTextCatalog.Key("limit.option.on") ? "Вкл" : original);
        menu.Open("status-renzokuken");
        Assert.Contains("Авто: Вкл", menu.CurrentScreen.Details(menu));
        Assert.Equal("On", character.LimitSettings["Auto"]);
        Assert.Equal("Off", character.LimitSettings["Indicator"]);
        Assert.Equal("Chosen name", character.Name);
    }

    [Fact]
    public void OpenMagicReviewRereadsItsSemanticPresentationProvider()
    {
        MenuController menu = new(() => new MenuData());
        string line = "First generation";
        menu.ShowMagicRulesReview(() => new[] { line }, () => { });
        Assert.Equal("First generation", Assert.Single(menu.CurrentScreen.Details(menu)));
        line = "次の世代";
        Assert.Equal("次の世代", Assert.Single(menu.CurrentScreen.Details(menu)));
        Assert.Equal(new[] { "magic-rules-apply", "magic-rules-cancel" }, menu.Entries.Select(entry => entry.Action!.Kind));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SemanticAliasesRespectStackLayerPriorityAndFlattenedExport(bool legacyInHighestLayer)
    {
        const string semanticId = "ui.connectguardianforceeditor.entry.label.cancel";
        string semanticKey = SystemTextCatalog.Key(semanticId);
        string legacyKey = SystemTextCatalog.Key("common.cancel");
        string lowKey = legacyInHighestLayer ? semanticKey : legacyKey;
        string highKey = legacyInHighestLayer ? legacyKey : semanticKey;
        Dictionary<string, LocalizationValue> low = new() { [lowKey] = new("Lower layer", "base", "base") };
        Dictionary<string, LocalizationValue> high = new() { [highKey] = new("Higher layer", "patch", "patch") };
        Dictionary<string, LocalizationValue> merged = low.Concat(high).ToDictionary(pair => pair.Key, pair => pair.Value);
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        resources.Prepare(null, localization: new(merged, new IReadOnlyDictionary<string, LocalizationValue>[] { low, high })).Commit();
        Assert.Equal("Higher layer", resources.Localize(semanticKey, "Cancel"));
        Assert.Equal("Higher layer", SystemTextCatalog.Translate(resources.Localize, semanticId, "Cancel"));
        LocalizationRecord Row(string key, string? target) => new() { Category = "System", File = SystemTextCatalog.File, Key = key, Source = "Cancel", Target = target };
        LocalizationProject original = new() { Manifest = new() { ProjectId = "base", TargetLanguage = "test" }, Entries = [Row(semanticKey, low.GetValueOrDefault(semanticKey)?.Text), Row(legacyKey, low.GetValueOrDefault(legacyKey)?.Text)] };
        LocalizationProject patch = new() { Manifest = original.Manifest with { ProjectId = "patch", BaseProjectId = "base" }, Entries = [Row(semanticKey, high.GetValueOrDefault(semanticKey)?.Text), Row(legacyKey, high.GetValueOrDefault(legacyKey)?.Text)] };
        string originalTargets = JsonSerializer.Serialize(original.Entries.Select(row => row.Target));
        string patchTargets = JsonSerializer.Serialize(patch.Entries.Select(row => row.Target));
        LocalizationProject flattened = LocalizationProjectExport.Resolve([original, patch], "flattened");
        LocalizationCatalog flat = new(flattened.Entries.Where(row => !string.IsNullOrEmpty(row.Target)).ToDictionary(row => row.Key, row => new LocalizationValue(row.Target!, "flat", "flat")));
        resources.Prepare(null, localization: flat).Commit();
        Assert.Equal("Higher layer", SystemTextCatalog.Translate(resources.Localize, semanticId, "Cancel"));
        Assert.Equal(originalTargets, JsonSerializer.Serialize(original.Entries.Select(row => row.Target)));
        Assert.Equal(patchTargets, JsonSerializer.Serialize(patch.Entries.Select(row => row.Target)));
    }

    [Fact]
    public void LegacyOnlyCommonIdentityRemainsAvailableForExpandedSemanticCallsites()
    {
        Dictionary<string, string> targets = new()
        {
            [SystemTextCatalog.Key("common.cancel")] = "Legacy cancel"
        };
        MenuController menu = new(() => new MenuData());
        menu.ConnectLocalizationText((key, original) => targets.GetValueOrDefault(key, original));
        Assert.Equal("Legacy cancel", menu.SystemText("ui.connectguardianforceeditor.entry.label.cancel", "Cancel"));
        targets[SystemTextCatalog.Key("ui.connectguardianforceeditor.entry.label.cancel")] = "New cancel";
        Assert.Equal("New cancel", menu.SystemText("ui.connectguardianforceeditor.entry.label.cancel", "Cancel"));
    }

    [Fact]
    public void OriginalSaveLossDescriptionsUseStructuredArgumentsWithoutParsingSourceText()
    {
        NativeGuardianForceLegacyLoss loss = new(4, 9, "Remap", "Diagnostic source is independent") { DescriptionId = "remap", DescriptionArguments = [4, 9, 2, 7] };
        string translated = GuardianForceLegacyExportReview.LossText(loss, (key, original) => key == SystemTextCatalog.Key("save.gf-export.loss.remap") ? "{1}: {3} ← {2} (GF {0})" : original);
        Assert.Equal("9: 7 ← 2 (GF 4)", translated);
        Assert.Equal("Diagnostic source is independent", loss.Description);
        Assert.Equal(new[] { 4, 9, 2, 7 }, loss.DescriptionArguments);
        Assert.Equal("Diagnostic source is independent", GuardianForceLegacyExportReview.LossText(loss with { DescriptionId = null }));
    }

    [Fact]
    public void DebugTranslationsPreserveGroupSelectionAndReadCurrentTemplates()
    {
        BattleDefinition initial = new() { Combatants = [new() { Id = "party", Name = "Player text", IsPlayer = true }, BattleDebugDefaults.Dummy] };
        BattleDebugController editor = new(initial, new BattleDebugCatalog(), BattleAtbMode.Normal, (_, _) => { }, () => { });
        string state = JsonSerializer.Serialize(editor.Definition);
        string maximum = "{0}最大HP";
        editor.LocalizeText = (key, original) => key == SystemTextCatalog.Key("debug.addactor.label.actor.details") ? "分類" : key == SystemTextCatalog.Key("debug.actor.label.0.maximum.hp") ? maximum : original;
        editor.Select(editor.Rows.ToList().FindIndex(row => row.Label == "Editor panel"), false);
        editor.Navigate(MenuInput.Right);
        editor.Select(editor.Rows.ToList().FindIndex(row => row.Value == "Player text"), false);
        editor.Navigate(MenuInput.Confirm);
        editor.Select(editor.Rows.ToList().FindIndex(row => row.Label == "分類"), false);
        editor.Navigate(MenuInput.Right);
        Assert.Contains(editor.Rows, row => row.Label.Contains("最大HP", StringComparison.Ordinal));
        maximum = "HP最大 {0}";
        Assert.Contains(editor.Rows, row => row.Label.StartsWith("HP最大 ", StringComparison.Ordinal));
        Assert.Equal(state, JsonSerializer.Serialize(editor.Definition));
    }

    [Fact]
    public void InspectorTranslationKeepsOriginalCommandIdentityAndPlayerText()
    {
        SceneInspectorSnapshot snapshot = new(1, "Native field", 7, [new("layer-42", SceneInspectorKind.Layer, "Source label", true, [])]);
        SceneInspectorCommand? sent = null;
        SceneInspectorController inspector = new(() => snapshot, command => sent = command, () => { });
        string target = "{0} — {1}";
        inspector.LocalizeText = (key, original) => key == SystemTextCatalog.Key("inspector.message.toggle") ? target : original;
        inspector.ToggleSelected();
        Assert.Equal(new SceneInspectorCommand("layer-42", false), sent);
        Assert.Equal("Source label — disabled", inspector.Message);
        target = "[{1}] {0}";
        Assert.Equal("[disabled] Source label", inspector.Message);
        Assert.Equal("layer-42", inspector.Selected!.Id);
        Assert.Equal("Source label", inspector.Selected.Name);
    }

    [Fact]
    public void OpenMenuReadsNewTemplatesWithoutChangingCommandOrPlayerIdentity()
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-1", Name = "Player text", Quantity = 3 });
        MenuController menu = new(() => data);
        Dictionary<string, string> targets = new()
        {
            [SystemTextCatalog.Key("screen.item-target.title")] = "対象 😀",
            [SystemTextCatalog.Key("ui.register.details.item")] = "Предмет: {0}"
        };
        menu.ConnectLocalizationText((key, original) => targets.GetValueOrDefault(key, original));
        menu.Open("item-target");
        Assert.Equal("対象 😀", menu.CurrentScreen.Title);
        Assert.Contains("Предмет: Player text", menu.CurrentScreen.Details(menu));
        targets[SystemTextCatalog.Key("ui.register.details.item")] = "{0} — 選択";
        Assert.Contains("Player text — 選択", menu.CurrentScreen.Details(menu));
        Assert.Equal("item-1", menu.Item!.Id);
        Assert.Equal("Player text", menu.Item.Name);
        Assert.Equal(3, menu.Item.Quantity);
    }

    [Fact]
    public void CompositeTemplatesReorderArgumentsUseCurrentCultureAndRejectInvalidFormat()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            MenuController menu = new(() => new MenuData());
            string target = "{1} : {0:N1}";
            menu.ConnectLocalizationText((_, _) => target);
            Assert.Equal("Stock : 12,5", menu.FormatSystemText("synthetic.quantity", "{0:N1} {1}", 12.5m, "Stock"));
            target = "{99}";
            Assert.Equal("12,5 Stock", menu.FormatSystemText("synthetic.quantity", "{0:N1} {1}", 12.5m, "Stock"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ExistingBattlePromptReadsCommittedGenerationWithoutMutatingBattleState()
    {
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Chosen name", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
        hud.ConfigureLocalization(new(resources));
        hud.Attack();
        string state = JsonSerializer.Serialize(battle.Capture());
        string key = SystemTextCatalog.Key("battle.ui.attack.waiting.for.a.character.s.atb.gauge");
        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("待機 😀", "test", "test") })).Commit();
        Assert.Equal("待機 😀", hud.Message);
        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("Ожидание", "test", "test") })).Commit();
        Assert.Equal("Ожидание", hud.Message);
        Assert.Equal(state, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void ExistingEmptyQuickLoadPromptUsesNewGeneration()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.SystemText.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "session.json"), Path.Combine(directory, "saves"));
            PlayerSaveMenu menu = new(store);
            string target = "Нет сохранений";
            menu.LocalizeText = (key, original) => key == SystemTextCatalog.Key("save.open.no.quick.saves") ? target : original;
            menu.Open(PlayerSaveMenuMode.QuickLoad);
            Assert.Equal("Нет сохранений", menu.Message);
            target = "セーブなし";
            Assert.Equal("セーブなし", menu.Message);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
