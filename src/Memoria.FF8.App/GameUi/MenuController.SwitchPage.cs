using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private const int SwitchReserveColumns = 4;
    internal static readonly string EmptySwitchSlotId = $"character-{Memoria.FF8.Events.NativePartySwitchInput.EmptySlot}";
    internal string SwitchEmptySlotLabel
    {
        get
        {
            if (Data.PartyCapacity <= 3)
            {
                return SystemText("party.switch.empty-slot", "Empty slot");
            }
            if (ScreenId == "switch-party-target")
            {
                return Data.Characters.Any(character => character.Id == ExchangeId && character.InParty)
                    ? SystemText("party.switch.remove-member", "Remove from party")
                    : SystemText("party.switch.add-member", "Add to party");
            }
            return Data.SupportedActions.Contains("party-switch-add-slot")
                ? SystemText("party.switch.add-member", "Add to party")
                : SystemText("party.switch.remove-member", "Remove from party");
        }
    }

    private bool ShowSwitchEmptySlot => (ScreenId is "switch-party" or "switch-party-target") && Data.SupportedActions.Contains("party-switch-empty-slot") && (Data.PartyCapacity <= 3 || ScreenId != "switch-party-target" || ExchangeId != EmptySwitchSlotId);

    private int SwitchPartyColumns(int count) => Data.PartyCapacity > 3 && count > 4 ? 2 : 1;
    private static int SwitchReserveRows(int count) => Math.Max(2, (count + SwitchReserveColumns - 1) / SwitchReserveColumns);

    private bool NavigateNativeSwitch(MenuInput input)
    {
        if (ScreenId is not ("switch-party" or "switch-party-target" or "switch-junction" or "switch-junction-target") || input is not (MenuInput.Left or MenuInput.Right or MenuInput.Up or MenuInput.Down))
        {
            return false;
        }
        MenuCharacter[] party = Data.Characters.Where(character => character.InParty).ToArray();
        MenuCharacter[] reserves = Data.Characters.Where(character => !character.InParty).ToArray();
        int partyColumns = SwitchPartyColumns(party.Length);
        (int X, int Y) Cell(MenuEntry? entry)
        {
            if (NativeSwitchCharacterId(entry) == EmptySwitchSlotId)
            {
                return Data.PartyCapacity > 3 ? (partyColumns, SwitchReserveRows(reserves.Length)) : (0, party.Length);
            }
            MenuCharacter? character = NativeSwitchCharacter(entry);
            int index = Array.IndexOf(party, character);
            if (index >= 0)
            {
                return (index % partyColumns, index / partyColumns);
            }
            index = Array.IndexOf(reserves, character);
            return (partyColumns + index % SwitchReserveColumns, index / SwitchReserveColumns);
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
        UiElement reserveMembers = NativeSwitchMembers(reserves, true, !root);
        if (Data.PartyCapacity > 3 && ShowSwitchEmptySlot)
        {
            var reserveArea = Grid([4, 1], [1], 4);
            Place(reserveArea, reserveMembers, 0);
            int slotIndex = Entries.ToList().FindIndex(entry => NativeSwitchCharacterId(entry) == EmptySwitchSlotId);
            if (slotIndex >= 0)
            {
                UiButton vacancy = new() { Id = EmptySwitchSlotId, Text = SwitchEmptySlotLabel, FontSize = 19 * TextScale, Enabled = Entries[slotIndex].Enabled, Click = () => Activate(slotIndex) };
                Place(reserveArea, vacancy, 1);
            }
            reserveMembers = reserveArea;
        }
        Place(right, Panel(null, reserveMembers), 0);
        MenuCharacter? selected = root ? Character : NativeSwitchCharacter(SelectedEntry);
        IReadOnlyList<string> details = !root && NativeSwitchCharacterId(SelectedEntry) == EmptySwitchSlotId ? [SwitchEmptySlotLabel] : [selected?.Name ?? "--", FormatSystemText("ui.buildnativeswitch.details.lv", "Lv  {0}", Number(selected?.Level)), FormatSystemText("ui.buildnativeswitch.details.hp.template", "HP  {0}", (selected is null ? "--" : Hp(selected)))];
        Place(right, Panel(null, Lines(details, 25)), 1);
        Place(body, right, 0, 1);
        Place(page, body, 1);
        return page;
    }

    private MenuCharacter? NativeSwitchCharacter(MenuEntry? entry)
    {
        string? id = NativeSwitchCharacterId(entry);
        return Data.Characters.FirstOrDefault(character => character.Id == id);
    }

    private static string? NativeSwitchCharacterId(MenuEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }
        return entry.Action?.Kind is "party-switch" or "junction-exchange" ? entry.Action.TargetId : entry.Action?.SubjectId ?? entry.Id;
    }

    private UiElement NativeSwitchMembers(MenuCharacter[] characters, bool reserves, bool interactive)
    {
        bool emptySlot = !reserves && Data.PartyCapacity <= 3 && ShowSwitchEmptySlot;
        int count = characters.Length + (emptySlot ? 1 : 0);
        int columns = reserves ? SwitchReserveColumns : SwitchPartyColumns(count);
        int rows = reserves ? SwitchReserveRows(count) : Data.PartyCapacity == 3 ? 3 : Math.Max(3, (count + columns - 1) / columns);
        float memberFontSize = columns > 1 ? 17 : rows > 3 ? 21 : 24;
        var members = Grid(Enumerable.Repeat(1f, rows).ToArray(), Enumerable.Repeat(1f, columns).ToArray(), reserves ? 6 : 4);
        for (int index = 0; index < count; index++)
        {
            MenuCharacter? character = index < characters.Length ? characters[index] : null;
            string id = character?.Id ?? EmptySwitchSlotId;
            var row = reserves ? Grid([4, 1], [1], 2) : Grid([1], [1, 2.7f], columns > 1 ? 5 : 10);
            if (character is not null)
            {
                Place(row, Portrait(character), 0);
            }
            if (character is null)
            {
                Place(row, Label(SwitchEmptySlotLabel, memberFontSize), 0, 1);
            }
            else if (reserves)
            {
                Place(row, Label(character.Name, 19), 1);
            }
            else
            {
                Place(row, Lines([character.Name, FormatSystemText("ui.nativeswitchmembers.details.lv", "Lv  {0}", Number(character.Level)), FormatSystemText("ui.nativeswitchmembers.details.hp", "HP  {0}", Hp(character))], memberFontSize), 0, 1);
            }
            UiElement member = row;
            if (interactive)
            {
                int entryIndex = Entries.ToList().FindIndex(entry => NativeSwitchCharacterId(entry) == id);
                if (entryIndex >= 0)
                {
                    UiButton choice = new() { Id = id, DrawBackground = false, HighlightSelection = false, Enabled = Entries[entryIndex].Enabled, Click = () => Activate(entryIndex) };
                    if (!choice.Enabled)
                    {
                        DisableSwitchContent(row);
                    }
                    row.Margin = new UiThickness(reserves ? 10 : columns > 1 ? 14 : 24, 0, 0, 0);
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
