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
