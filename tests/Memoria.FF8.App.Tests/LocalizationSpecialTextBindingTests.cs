using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationSpecialTextBindingTests
{
    [Fact]
    public void RichFormattingPreservesCanonicalTemplateAndCapturedNameSubstitutions()
    {
        bool wasEnabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            const string template = "{Color Red}{Char Squall}{Line}Ready{Next}Again";
            LocalizationTextBinding binding = new("$en_test_0001", template, template, template);
            string bound = LocalizationTextTrace.Attach(template, binding);
            DialogueDocument document = DialogueTextFormatter.Parse(bound, new Dictionary<int, string> { [GameNames.CharacterSubject(0)] = "Leon" }, originalCompatible: true);
            Assert.Equal("$en_test_0001", document.Binding!.Key);
            Assert.Equal(template, document.Binding.Template);
            Assert.Equal("Leon\nReady\nAgain", document.Binding.Text);
            Assert.NotEmpty(document.Binding.Arguments);
            Assert.Equal(binding.Key, LocalizationTextTrace.Find(document.Text)!.Key);
            Assert.Equal(binding.Key, LocalizationTextTrace.Find(DialogueTextFormatter.FormatWithPrompts(bound))!.Key);
            Assert.Equal(binding.Key, document.WithLiteralLine("Diagnostic").Binding!.Key);
        }
        finally
        {
            LocalizationTextTrace.Enabled = wasEnabled;
        }
    }

    [Fact]
    public void BattleOccurrencesUseNativeEventIdentityWhileInspectionIsOff()
    {
        bool wasEnabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = false;
        try
        {
            NativeBattleAiDefinition ai = new([[1, 0, 1, 0, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["{Char Squall} ready"]);
            BattleSession battle = new(new BattleDefinition { NativeMessageSpeed = 3, Combatants = [new() { Id = "party-0", Name = "Leon", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", ModelId = 22, NativeAi = ai }] }, BattleAtbMode.TurnBased);
            battle.Tick();
            using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
            ResourceService resources = new(source);
            BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
            hud.ConfigureLocalization(new(resources));
            List<(LocalizationTextBinding Binding, string Channel, string Id, string Text)> occurrences = [];
            hud.DialoguePresented += (binding, channel, id, text) => occurrences.Add((binding, channel, id, text));
            hud.AdvancePresentation(TimeSpan.Zero);
            Assert.Single(occurrences);
            string key = occurrences[0].Binding.Key;
            Assert.Equal("Leon ready", occurrences[0].Text);
            for (int draw = 0; draw < 4; draw++)
            {
                Assert.Equal(key, hud.BattleMessageDocument!.Binding!.Key);
                hud.BuildView();
                hud.AdvancePresentation(TimeSpan.Zero);
            }
            Assert.Single(occurrences);
            hud.AdvancePresentation(TimeSpan.FromSeconds(1));
            Assert.Equal(2, occurrences.Count);
            Assert.Equal(key, occurrences[1].Binding.Key);
            Assert.NotEqual(occurrences[0].Id, occurrences[1].Id);
            resources.Prepare(null, localization: new LocalizationCatalog(new Dictionary<string, LocalizationValue> { [key] = new("Перевод", "mod", "project") })).Commit();
            Assert.Equal("Перевод", hud.BattleMessageDocument!.Text);
            hud.AdvancePresentation(TimeSpan.Zero);
            Assert.Equal(2, occurrences.Count);
            Assert.Equal("Leon ready", occurrences[0].Text);
        }
        finally
        {
            LocalizationTextTrace.Enabled = wasEnabled;
        }
    }

    [Fact]
    public void SameVisibleTutorialWordingRetainsTheNewCanonicalRowIdentity()
    {
        bool wasEnabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            MenuController menu = new(() => new MenuData(), _ => { });
            string first = LocalizationTextTrace.Attach("Same", new("row-one", "Same", "Same", "Same"));
            string second = LocalizationTextTrace.Attach("Same", new("row-two", "Same", "Same", "Same"));
            menu.ShowTutorialMessage(first, 192, 104);
            menu.ShowTutorialMessage(second, 192, 104);
            var field = typeof(MenuController).GetField("_tutorialMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.Equal("row-two", LocalizationTextTrace.Find((string)field.GetValue(menu)!)!.Key);
        }
        finally
        {
            LocalizationTextTrace.Enabled = wasEnabled;
        }
    }
}
