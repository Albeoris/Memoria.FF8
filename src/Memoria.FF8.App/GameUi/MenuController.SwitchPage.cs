using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool NavigateNativeSwitch(MenuInput input)
    {
        if (ScreenId is not ("switch-party" or "switch-party-target" or "switch-junction" or "switch-junction-target") || input is not (MenuInput.Left or MenuInput.Right or MenuInput.Up or MenuInput.Down))
        {
            return false;
        }
        MenuCharacter[] party = Data.Characters.Where(character => character.InParty).ToArray();
        MenuCharacter[] reserves = Data.Characters.Where(character => !character.InParty).ToArray();
        (int X, int Y) Cell(MenuEntry? entry)
        {
            MenuCharacter? character = NativeSwitchCharacter(entry);
            int index = Array.IndexOf(party, character);
            if (index >= 0)
            {
                return (0, index);
            }
            index = Array.IndexOf(reserves, character);
            return (1 + index % 4, index / 4);
        }
        (int x, int y) = Cell(SelectedEntry);
        bool horizontal = input is MenuInput.Left or MenuInput.Right;
        int direction = input is MenuInput.Left or MenuInput.Up ? -1 : 1;
        int next = SelectedIndex;
        int best = int.MaxValue;
        IReadOnlyList<MenuEntry> entries = Entries;
        for (int index = 0; index < entries.Count; index++)
        {
            if (!entries[index].Enabled)
            {
                continue;
            }
            (int targetX, int targetY) = Cell(entries[index]);
            int primary = horizontal ? (targetX - x) * direction : (targetY - y) * direction;
            int secondary = horizontal ? Math.Abs(targetY - y) : Math.Abs(targetX - x);
            int distance = secondary * 100 + primary;
            if (primary > 0 && distance < best)
            {
                best = distance;
                next = index;
            }
        }
        Select(next, entries.Count);
        return true;
    }

    private UiElement BuildNativeSwitch()
    {
        bool root = ScreenId == "switch";
        var page = Grid([1.3f, 4.7f], [1], 4);
        UiElement strip;
        if (root)
        {
            var commands = Commands(38, 25);
            commands.Orientation = UiOrientation.Horizontal;
            commands.Items = Entries.Select(entry => entry.Id == "switch-party" ? SystemText("party.switch.member", "Switch Member") : entry.Label).ToList();
            strip = commands;
        }
        else
        {
            strip = Label(ScreenId.StartsWith("switch-party", StringComparison.Ordinal) ? SystemText("party.switch.member", "Switch Member") : SystemText("ui.buildnativeswitch.label.junction.exchange", "Junction Exchange"), 25);
        }
        Place(page, NativeMenuHeader(SystemText("ui.buildnativeswitch.nativemenuheader.switch", "Switch"), strip), 0);

        var body = Grid([1], [1, 1], 4);
        Place(body, Panel(null, NativeSwitchMembers(Data.Characters.Where(character => character.InParty).ToArray(), false, !root)), 0);

        var right = Grid([3, 1], [1], 4);
        MenuCharacter[] reserves = Data.Characters.Where(character => !character.InParty).ToArray();
        Place(right, Panel(null, NativeSwitchMembers(reserves, true, !root)), 0);
        MenuCharacter? selected = root ? Character : NativeSwitchCharacter(SelectedEntry);
        Place(right, Panel(null, Lines([selected?.Name ?? "--", FormatSystemText("ui.buildnativeswitch.details.lv", "Lv  {0}", Number(selected?.Level)), FormatSystemText("ui.buildnativeswitch.details.hp.template", "HP  {0}", (selected is null ? "--" : Hp(selected)))], 25)), 1);
        Place(body, right, 0, 1);
        Place(page, body, 1);
        return page;
    }

    private MenuCharacter? NativeSwitchCharacter(MenuEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }
        string id = entry.Action?.Kind is "party-switch" or "junction-exchange" ? entry.Action.TargetId : entry.Action?.SubjectId ?? entry.Id;
        return Data.Characters.FirstOrDefault(character => character.Id == id);
    }

    private UiElement NativeSwitchMembers(MenuCharacter[] characters, bool reserves, bool interactive)
    {
        int columns = reserves ? 4 : 1;
        int rows = reserves ? 2 : 3;
        var members = Grid(Enumerable.Repeat(1f, rows).ToArray(), Enumerable.Repeat(1f, columns).ToArray(), reserves ? 6 : 4);
        for (int index = 0; index < characters.Length; index++)
        {
            MenuCharacter character = characters[index];
            var row = reserves ? Grid([4, 1], [1], 2) : Grid([1], [1, 2.7f], 10);
            Place(row, Portrait(character), 0);
            if (reserves)
            {
                Place(row, Label(character.Name, 19), 1);
            }
            else
            {
                Place(row, Lines([character.Name, FormatSystemText("ui.nativeswitchmembers.details.lv", "Lv  {0}", Number(character.Level)), FormatSystemText("ui.nativeswitchmembers.details.hp", "HP  {0}", Hp(character))], 24), 0, 1);
            }
            UiElement member = row;
            if (interactive)
            {
                int entryIndex = Entries.ToList().FindIndex(entry => NativeSwitchCharacter(entry)?.Id == character.Id);
                if (entryIndex >= 0)
                {
                    UiButton choice = new() { Id = character.Id, DrawBackground = false, HighlightSelection = false, Enabled = Entries[entryIndex].Enabled, Click = () => Activate(entryIndex) };
                    if (!choice.Enabled)
                    {
                        DisableSwitchContent(row);
                    }
                    row.Margin = new UiThickness(reserves ? 10 : 24, 0, 0, 0);
                    choice.Children.Add(row);
                    member = choice;
                }
            }
            Place(members, member, index / columns, index % columns);
        }
        return members;
    }

    private static void DisableSwitchContent(UiElement element)
    {
        element.Enabled = false;
        foreach (UiElement child in element.Children)
        {
            DisableSwitchContent(child);
        }
    }
}
