using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiElement BuildNativeGuardianForce()
    {
        var page = NativeCollectionPage(SystemText("ui.buildnativeguardianforce.nativecollectionpage.gf", "GF"), ScreenId is "gf-learn" or "gf-abilities" ? Label(ScreenId == "gf-learn" ? SystemText("ui.buildnativeguardianforce.label.learn", "Learn") : SystemText("ui.buildnativeguardianforce.label.abilities", "Abilities"), 28) : null);
        if (ScreenId == "gf")
        {
            var tiles = new UiGrid { Columns = 8, Rows = 2, Gap = 8 };
            int first = SelectedIndex / 16 * 16;
            for (int index = first; index < Math.Min(first + 16, Entries.Count); index++)
            {
                int selected = index;
                MenuEntry entry = Entries[index];
                MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(gf => gf.Id == entry.Id);
                var button = new UiButton { Id = entry.Id, DrawBackground = false, HighlightSelection = false, PointerSize = 20 * TextScale, Enabled = entry.Enabled, Click = () => Activate(selected) };
                var tile = Grid([4, 1], [1], 0);
                tile.Margin = new UiThickness(35, 12, 0, 8);
                Place(tile, Artwork(UiImageKind.GuardianForcePortrait, force?.NativeId), 0);
                Place(tile, Label(FormatSystemText("ui.buildnativeguardianforce.label.lv", "Lv {0}", Number(force?.Level)), 21), 1);
                button.Children.Add(tile);
                Place(tiles, button, (index - first) / 8, (index - first) % 8);
            }
            Place(page, Panel(SystemText("ui.buildnativeguardianforce.panel.g.f", "G.F."), tiles), 2);
            return page;
        }

        var body = Grid([1], [1, 1.9f], 3);
        var summary = Grid([2.1f, 1.5f, 1.5f], [1], 8);
        var identity = Grid([1], [1, 1.9f], 8);
        Place(identity, Artwork(UiImageKind.GuardianForcePortrait, GuardianForce?.NativeId), 0, 0);
        Place(identity, Readouts([GuardianForce?.Name ?? "--", FormatSystemText("ui.buildnativeguardianforce.details.lv", "Lv    {0}", Number(GuardianForce?.Level)), FormatSystemText("ui.buildnativeguardianforce.details.hp", "HP    {0}/{1}", Number(GuardianForce?.CurrentHp), Number(GuardianForce?.MaximumHp))], 25), 0, 1);
        Place(summary, identity, 0);
        Place(summary, Readouts([FormatSystemText("ui.buildnativeguardianforce.details.current.exp", "Current EXP    {0}", Number(GuardianForce?.Experience)), FormatSystemText("ui.buildnativeguardianforce.details.next.level", "Next LEVEL    {0}", Number(GuardianForce?.NextLevelExperience))], 24), 1);
        MenuAbilityChoice? learning = GuardianForce?.LearningChoices.FirstOrDefault(choice => GuardianForce.LearningAbilityId is { } id ? choice.Id == id : choice.Name == GuardianForce.Learning);
        Place(summary, Readouts([SystemText("ui.buildnativeguardianforce.details.learning", "Learning"), GuardianForce?.Learning ?? "--", learning is null ? FormatSystemText("ui.buildnativeguardianforce.details.ap", "AP    {0}", Number(GuardianForce?.AbilityPoints)) : FormatSystemText("ui.buildnativeguardianforce.details.ap.alternate", "{0} / {1} AP", learning.Progress, learning.RequiredPoints)], 25), 2);
        Place(body, summary, 0, 0);
        MenuAbilityChoice[] choices = GuardianForce?.LearningChoices.ToArray() ?? [];
        UiList abilities = NativePageCommands(false, 39, 25);
        abilities.EnabledItems = [];
        abilities.Paginated = true;
        abilities.PageSize = 10;
        abilities.FitPageRows = true;
        abilities.AutoSecondaryWidth = true;
        abilities.SecondaryItems = choices.Select(choice => choice.IsPassive && choice.Learned ? FormatSystemText("guardian.passive.state", "{0} / {1}", choice.PassiveEnabled ? SystemText("guardian.passive.on", "On") : SystemText("guardian.passive.off", "Off"), choice.PassiveAvailable ? SystemText("guardian.passive.available", "Available") : SystemText("guardian.passive.ko", "GF KO")) : choice.Learned ? SystemText("guardian.learning.complete", "Complete!") : $"{choice.Progress} / {choice.RequiredPoints}").ToList();
        string guardianForceId = GuardianForce?.Id ?? "";
        abilities.ItemTextColor = index => index < choices.Length
            ? choices[index] is { IsPassive: true, Learned: true } passive
                ? passive.PassiveEnabled ? new UiColor(112, 232, 144) : new UiColor(232, 184, 96)
                : BattleLearnedAbilityColor(guardianForceId, choices[index].Id)
            : null;
        abilities.Images = choices.Select(choice => choice.Icon).ToList();
        abilities.LeadingImages = choices.Select(choice => choice == learning && !choice.Learned ? (UiImageSource?)new UiImageSource(UiImageKind.Icon, 207) : null).ToList();
        var abilityPanel = Panel(SystemText("ui.buildnativeguardianforce.panel.ability", "ABILITY"), abilities);
        abilityPanel.Id = "gf-abilities-panel";
        Place(body, abilityPanel, 0, 1);
        Place(page, body, 2);
        return page;
    }

    private UiElement BuildNativeCard()
    {
        MenuCard? card = ScreenId == "card" ? Data.Cards.FirstOrDefault(value => value.Id == SelectedEntry?.Id) : Card;
        var page = Grid([0.85f * Math.Max(1, TextScale), 6.4f, 0.75f], [1], 3);
        var header = Grid([1], [2.9f, 1], 3);
        Place(header, Panel(SystemText("ui.buildnativecard.panel.help", "HELP"), Label(Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help, 23, UiTextFlow.Wrap)), 0);
        Place(header, Panel(null, Label(Data.CardCollectionComplete == true ? SystemText("ui.buildnativecard.label.card", "Card ★") : SystemText("ui.buildnativecard.label.card.alternate", "Card"), 31)), 0, 1);
        Place(page, header, 0);
        var body = Grid([1], [1, 1], 3);
        if (ScreenId == "card")
        {
            var list = NativePageCommands(false, 42, 28);
            list.Items = Entries.Select(entry => Data.Cards.FirstOrDefault(value => value.Id == entry.Id)?.Name ?? entry.Label).ToList();
            list.SecondaryItems = Entries.Select(entry => Data.Cards.FirstOrDefault(value => value.Id == entry.Id)?.Quantity.ToString() ?? "--").ToList();
            list.AutoSecondaryWidth = true;
            Place(body, Panel(SystemText("ui.buildnativecard.panel.cards", "CARDS"), list), 0);
        }
        else
        {
            Place(body, Panel(SystemText("ui.buildnativecard.panel.cards", "CARDS"), Readouts([card?.Name ?? "--", FormatSystemText("ui.buildnativecard.details.num", "NUM.    {0}", card?.Quantity.ToString() ?? "--"), FormatSystemText("ui.buildnativecard.details.level", "Level    {0}", Number(card?.Level)), FormatSystemText("ui.buildnativecard.details.ranks", "Ranks    {0}", card?.Ranks ?? "--"), FormatSystemText("ui.buildnativecard.details.element", "Element    {0}", card?.Element ?? "--")], 26)), 0);
        }
        var preview = Grid([1, 1.05f], [1], 3);
        Place(preview, Panel(null, Artwork(UiImageKind.Card, card?.NativeId)), 0);
        var totals = Grid([1, 1, 1, 1, 1], [3, 1], 0);
        string[] categories = ["MONSTER", "BOSS", "GF", "PLAYER", "TOTAL"];
        for (int index = 0; index < categories.Length; index++)
        {
            int category = index;
            IEnumerable<MenuCard> cards = Data.Cards.Where(value => value.Quantity > 0);
            if (category < 4)
            {
                cards = cards.Where(value => value.Level.HasValue && (category switch { 0 => value.Level <= 5, 1 => value.Level is 6 or 7, 2 => value.Level is 8 or 9, _ => value.Level == 10 }));
            }
            // The projected collection supplies owned quantities, not lifetime acquisition totals.
            Place(totals, Label(categories[index], 27), index, 0);
            Place(totals, Label(cards.Sum(value => value.Quantity).ToString(), 27), index, 1);
        }
        Place(preview, Panel(null, totals), 1);
        Place(body, preview, 0, 1);
        Place(page, body, 1);
        Place(page, Panel(null, Label(ScreenId == "card-detail" ? card?.Description ?? "" : "", 21, UiTextFlow.Wrap)), 2);
        return page;
    }

    private UiElement BuildNativeStatus()
    {
        if (ScreenId == "status-angelo" && !IsDemo)
        {
            return BuildNativeAngeloStatus();
        }
        bool primary = ScreenId is "status" or "status-stats" or "status-info";
        var page = NativeCollectionPage(SystemText("ui.buildnativestatus.nativecollectionpage.status", "Status"), Entries.Count > 0 ? NativePageCommands(true, 34, ScreenId == "status-renzokuken" && !IsDemo ? 20 : 23) : null);
        var body = Grid([2.6f, 1.05f, 2.4f], [1.1f, 0.9f], 3);
        var identity = Grid([1], [1, 2.3f], 12);
        Place(identity, Portrait(Character), 0, 0);
        Place(identity, Readouts([Character?.Name ?? "--", FormatSystemText("ui.buildnativestatus.details.lv", "Lv    {0}", Number(Character?.Level)), FormatSystemText("ui.buildnativestatus.details.hp", "HP    {0}", (Character is null ? "--" : Hp(Character)))], 28), 0, 1);
        Place(body, identity, 0, 0);
        var commandArea = Grid([1], [0.45f, 1], 0);
        Place(commandArea, Panel(SystemText("ui.buildnativestatus.panel.command", "COMMAND"), Readouts(Character?.Commands.Take(4).ToArray() ?? ["--"], 27)), 0, 1);
        Place(body, commandArea, 0, 1);
        Place(body, Readouts([FormatSystemText("ui.buildnativestatus.details.current.exp", "Current EXP    {0}", Number(Character?.Experience)), FormatSystemText("ui.buildnativestatus.details.next.level", "Next LEVEL    {0}", Number(Character?.NextLevelExperience))], 25), 1, 0);
        if (primary)
        {
            string[] keys = ["Str", "Vit", "Mag", "Spr", "Spd", "Luck", "Eva", "Hit"];
            var stats = Grid([1, 1, 1, 1], [1.3f, 0.7f, 1.3f, 0.7f], 0);
            for (int index = 0; index < keys.Length; index++)
            {
                Place(stats, Label(StatText(keys[index]), 25), index % 4, index / 4 * 2);
                Place(stats, Label(Character?.Stats.TryGetValue(keys[index], out int value) == true ? value.ToString() : "--", 25), index % 4, index / 4 * 2 + 1);
            }
            Place(body, stats, 2, 0);
            var weapon = Panel(null, Label(Character?.Weapon ?? "--", 27));
            weapon.Id = "status-weapon";
            Place(body, weapon, 1, 1);
            Place(body, Panel(SystemText("ui.buildnativestatus.panel.ability", "ABILITY"), Readouts(Character?.Abilities.ToArray() ?? ["--"], 25)), 2, 1);
        }
        else
        {
            Place(body, Panel(Character?.LimitBreak ?? SystemText("ui.buildnativestatus.panel.limit.break", "Limit Break"), Details()), 2, 0, 1, 2);
        }
        Place(page, body, 2);
        return page;
    }

    private UiElement BuildNativeAngeloStatus()
    {
        var page = NativeCollectionPage(SystemText("ui.buildnativeangelostatus.nativecollectionpage.status", "Status"), Label(Character?.LimitSettings.GetValueOrDefault("Angelo name", "Angelo") ?? SystemText("ui.buildnativeangelostatus.label.angelo", "Angelo"), 26));
        var body = Grid([1], [1, 2.4f], 10);
        var identity = Grid([1.4f, 1, 1.2f], [1], 10);
        Place(identity, Portrait(Character), 0);
        Place(identity, Readouts([Character?.Name ?? "--", FormatSystemText("ui.buildnativeangelostatus.details.lv", "Lv    {0}", Number(Character?.Level)), FormatSystemText("ui.buildnativeangelostatus.details.hp", "HP    {0}", (Character is null ? "--" : Hp(Character)))], 26), 1);
        Place(identity, Artwork(UiImageKind.AngeloPortrait, 0), 2);
        Place(body, identity, 0, 0);
        UiList abilities = NativePageCommands(false, 42, 25);
        abilities.Id = "angelo-abilities";
        abilities.PageSize = 8;
        abilities.FitPageRows = true;
        abilities.AutoSecondaryWidth = true;
        MenuLimitMove[] moves = Entries.Select(entry => Character!.LimitMoves.Single(move => move.Id == entry.Id)).ToArray();
        abilities.SecondaryItems = moves.Select(move => move.Learned == true ? SystemText("guardian.learning.complete", "Complete!") : move.TrainingProgress is int progress ? $"{progress * 100 / 64}%{(move.IsLearning ? SystemText("guardian.learning.active", " · Learning") : "")}" : "?").ToList();
        abilities.LeadingImages = moves.Select(move => move.IsLearning ? (UiImageSource?)new UiImageSource(UiImageKind.Icon, 207) : null).ToList();
        Place(body, Panel(SystemText("ui.buildnativeangelostatus.panel.ability", "ABILITY"), abilities), 0, 1);
        Place(page, body, 2);
        return page;
    }

    private UiGrid NativeCollectionPage(string title, UiElement? commands = null)
    {
        var page = Grid([0.55f * Math.Max(1, TextScale), 0.75f * Math.Max(1, TextScale), 6.7f], [1], 3);
        var header = Grid([1], [3, 1], 3);
        Place(header, Panel(null, commands ?? Label("", 24)), 0);
        Place(header, Panel(null, Label(title, 29)), 0, 1);
        Place(page, header, 0);
        string help = Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help;
        if (ScreenId is "gf-learn" or "gf-abilities" && SelectedEntry?.Action?.Kind == "gf-passive-toggle" && SelectedEntry.Enabled)
        {
            help += FormatSystemText("ui.nativecollectionpage.help.card.toggle.passive.on.off", " {0} (Card): Toggle passive On/Off.", CurrentBindingText(ControlAction.Card));
        }
        Place(page, Panel(SystemText("ui.nativecollectionpage.panel.help", "HELP"), Label(help, 23, UiTextFlow.Wrap)), 1);
        return page;
    }

    private UiList NativePageCommands(bool horizontal, float rowHeight = 39, float fontSize = 25)
    {
        return new UiList
        {
            TextFlow = UiTextFlow.SingleLine,
            Id = "commands",
            Items = Entries.Select(entry => entry.Label).ToList(),
            EnabledItems = Entries.Select(entry => entry.Enabled).ToList(),
            SelectedIndex = SelectedIndex,
            Orientation = horizontal ? UiOrientation.Horizontal : UiOrientation.Vertical,
            Paginated = !horizontal,
            HighlightSelection = false,
            PointerSize = 20 * TextScale,
            TextInset = 35 * TextScale,
            RowHeight = rowHeight * TextScale,
            FontSize = fontSize * TextScale,
            SelectionChanged = index => Select(index, Entries.Count),
            Confirmed = Activate
        };
    }
}
