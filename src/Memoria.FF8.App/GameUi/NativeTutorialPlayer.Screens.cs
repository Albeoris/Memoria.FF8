using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private void InitializeSettings()
    {
        settings["Auto"] = "Off";
        settings["Indicator"] = "On";
        settings["Duel-Auto"] = "Off";
        settings["Angelo learning"] = "0";
    }

    private void RegisterTutorialScreens()
    {
        void Register(string id, string title, Func<MenuController, IReadOnlyList<MenuEntry>> entries, Func<MenuController, IReadOnlyList<string>>? details = null, string? statusPresentation = null)
        {
            Menu.Register(new MenuScreenDefinition { Id = id, Title = title, Help = definition.Title, Layout = MenuLayout.Character, Entries = entries, Details = details ?? (_ => []), BuildContent = statusPresentation is null ? null : (controller, panel) => panel.Children.Add(BuildTutorialStatusPresentation(controller, statusPresentation)) });
        }
        if (definition.Descriptor.CallbackMode == 28)
        {
            Register("status", "Status", _ => [new("next", "Status", "", "status-elements")]);
            Register("status-elements", "Status", _ => [new("next", "Element / Status", "", "status-gf")], _ => TutorialElementStatusDetails(), "elements");
            Register("status-gf", "Status", _ => [new("next", "GF", "", "status-limit")], TutorialCompatibilityDetails, "gf");
            string limitScreen = definition.Id switch { 5 => "status-renzokuken", 6 => "status-duel", 7 => "status-angelo", _ => throw new InvalidOperationException() };
            Register("status-limit", "Status", _ => [new("next", "Limit Break", "", limitScreen)]);
            Register("status-renzokuken", "Renzokuken", c => [Setting("Auto", c.SystemText("tutorial.status.gunblade-auto", "Gunblade Auto")), Setting("Indicator", c.SystemText("tutorial.status.renzokuken-indicator", "Renzokuken Indicator"))], c => c.Character?.LimitMoves.Where(move => move.Learned == true).Select(move => move.Name).ToArray() ?? []);
            Register("status-duel", "Duel", c => [Setting("Duel-Auto", c.LimitSettingLabel("Duel-Auto"))], _ => TutorialDuelDetails(), "duel");
            Register("status-angelo", "Angelo", _ => Enumerable.Range(0, 8).Where(index => (catalog.CreateIsolatedLimitProgress(definition.Id).PetPalsFlags!.Value & (1 << index)) != 0).Select(index => new MenuEntry(index.ToString(CultureInfo.InvariantCulture), OriginalAngeloName(index), "", Action: new MenuAction("tutorial-setting", "Angelo learning", Value: index.ToString(CultureInfo.InvariantCulture)))).ToArray(), _ => TutorialAngeloDetails(), "angelo");
            Register("status-angelo-warning", "Angelo", _ => [new("ok", "OK", "", "status-angelo")], _ => [Menu.FormatNativeTutorialText(catalog.StatusLabels[32].Value, names)]);
        }
        if (definition.Id == 8)
        {
            Register("switch", "Switch", _ => [new("members", "Switch Member", "", "switch-party"), new("junction", "Junction Exchange", "", "switch-junction")]);
            Register("switch-party", "Switch Member", c => SwitchChoices(c, "switch-party-target"));
            Register("switch-party-target", "Switch Member", c => SwitchTargets(c, "tutorial-party"));
            Register("switch-junction", "Junction Exchange", c => SwitchChoices(c, "switch-junction-target"));
            Register("switch-junction-target", "Junction Exchange", c => SwitchTargets(c, "tutorial-exchange-preview"));
            Register("switch-junction-confirm", "Junction Exchange", c => [new("yes", c.SystemText("junction.exchange.yes", "Yes"), "", Action: new MenuAction("tutorial-exchange", settings.GetValueOrDefault("Exchange source", "1"), settings.GetValueOrDefault("Exchange target", "3"))), new("no", c.SystemText("junction.exchange.no", "No"), "", "switch-junction")], c => [Menu.FormatNativeTutorialText(catalog.SwitchLabels[12].Value.Replace("{Dialog SelectedCharacter}", c.Data.Characters.FirstOrDefault(character => character.Id == settings.GetValueOrDefault("Exchange source"))?.Name ?? "", StringComparison.Ordinal).Replace("{Dialog SelectedMagic}", c.Data.Characters.FirstOrDefault(character => character.Id == settings.GetValueOrDefault("Exchange target"))?.Name ?? "", StringComparison.Ordinal), names)]);
        }
    }

    private string? GuardianForceLessonMessage() => textId switch
    {
        11 => Menu.SystemText("tutorial.gf.learning-screen", "Selecting a GF opens its Learn screen."),
        12 => Menu.SystemText("tutorial.gf.summary", "The summary shows its level, HP, experience and current learning target."),
        13 => Menu.SystemText("tutorial.gf.ability-list", "Browse the ability list beside the summary to choose what the GF learns next."),
        15 => Menu.SystemText("tutorial.gf.learned", "Str-J is already learned. Completed abilities show a completion or passive status."),
        16 => Menu.SystemText("tutorial.gf.unlearned", "Abilities you can still learn show an AP total beside their name."),
        17 => Menu.SystemText("tutorial.gf.first-ap-number", "The first AP number"),
        24 => Menu.SystemText("tutorial.gf.learning-summary", "The selected ability now appears under Learning."),
        _ => null
    };

    private MenuEntry Setting(string id, string label)
    {
        bool automaticIndicator = id == "Indicator" && settings["Auto"] == "On";
        string value = automaticIndicator ? "Off" : settings[id];
        string display = value == "On" ? Menu.SystemText("limit.option.on", "On") : Menu.SystemText("limit.option.off", "Off");
        return new MenuEntry(id, $"{label}   {display}", "", Action: new MenuAction("tutorial-setting", id, Value: value == "On" ? "Off" : "On"), Enabled: !automaticIndicator);
    }

    private IReadOnlyList<MenuEntry> SwitchChoices(MenuController controller, string destination) => controller.Data.Characters.Select(character => new MenuEntry(character.Id, character.Name, "", destination, new MenuAction("select-exchange", character.Id))).ToArray();

    private IReadOnlyList<MenuEntry> SwitchTargets(MenuController controller, string kind) => controller.Data.Characters.Select(character => new MenuEntry(character.Id, character.Name, "", Action: new MenuAction(kind, controller.TutorialExchangeId, character.Id))).ToArray();

    private NativeCharacterVitals ApplySetting(MenuAction action)
    {
        if (action.SubjectId is not ("Auto" or "Indicator" or "Duel-Auto" or "Angelo learning"))
        {
            throw new InvalidOperationException("Unknown original tutorial setting.");
        }
        settings[action.SubjectId] = action.Value;
        return party;
    }

    private NativeCharacterVitals SwitchParty(MenuAction action)
    {
        int source = int.Parse(action.SubjectId.AsSpan("character-".Length), CultureInfo.InvariantCulture);
        int target = int.Parse(action.TargetId.AsSpan("character-".Length), CultureInfo.InvariantCulture);
        byte[] next = party.CombatParty.ToArray();
        for (int slot = 0; slot < next.Length; slot++)
        {
            next[slot] = next[slot] == source ? (byte)target : next[slot] == target ? (byte)source : next[slot];
        }
        Menu.Open("switch-party");
        return (party with { CombatParty = next }).RebuildParty();
    }

    private NativeCharacterVitals PreviewExchange(MenuAction action)
    {
        settings["Exchange source"] = action.SubjectId;
        settings["Exchange target"] = action.TargetId;
        Menu.Push("switch-junction-confirm");
        Menu.SelectEntry("switch-junction-confirm", "no");
        return party;
    }

    private NativeCharacterVitals ExchangeJunctions(MenuAction action)
    {
        int Id(string value) => int.Parse(value.AsSpan(value.StartsWith("character-", StringComparison.Ordinal) ? "character-".Length : 0), CultureInfo.InvariantCulture);
        int source = Id(action.SubjectId);
        int target = Id(action.TargetId);
        NativeCharacterVital[] records = party.Records.ToArray();
        NativeCharacterVital first = records[source];
        NativeCharacterVital second = records[target];
        // Original4CB4A0 exchanges exactly stock+16..79 and junction+80..111;4BFD40 then clamps current HP.
        records[source] = first with { MagicInventory = second.MagicInventory.ToArray(), Junction = second.Junction with { MagicJunctions = second.Junction.MagicJunctions.ToArray() } };
        records[target] = second with { MagicInventory = first.MagicInventory.ToArray(), Junction = first.Junction with { MagicJunctions = first.Junction.MagicJunctions.ToArray() } };
        Menu.Open("switch-junction");
        return (party with { Records = records }).RebuildParty();
    }
}
