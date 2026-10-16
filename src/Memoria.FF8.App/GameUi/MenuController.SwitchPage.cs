using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiElement BuildNativeSwitch()
    {
        bool root = ScreenId == "switch";
        var page = Grid([1.3f, 4.7f], [1], 4);
        UiElement strip;
        if (root)
        {
            var commands = Commands(38, 25);
            commands.Orientation = UiOrientation.Horizontal;
            commands.Items = Entries.Select(entry => entry.Id == "switch-party" ? "Switch Member" : entry.Label).ToList();
            strip = commands;
        }
        else
        {
            strip = Label(ScreenId.StartsWith("switch-party", StringComparison.Ordinal) ? "Switch Member" : "Junction Exchange", 25);
        }
        Place(page, NativeMenuHeader("Switch", strip), 0);

        var body = Grid([1], [1, 1], 4);
        UiElement members;
        if (root)
        {
            members = NativeSwitchMembers(Data.Characters.Where(character => character.InParty).ToArray());
        }
        else
        {
            var choices = Commands(137, 23);
            choices.HighlightSelection = false;
            choices.EnabledItems = Entries.Select(entry => entry.Enabled).ToList();
            choices.Items = Entries.Select(entry => NativeSwitchCharacter(entry) is { } character ? $"{character.Name}\nLv  {Number(character.Level)}\nHP  {Hp(character)}" : entry.Label).ToList();
            choices.Images = Entries.Select(entry => NativeSwitchCharacter(entry)?.NativeId is { } nativeId ? (UiImageSource?)new UiImageSource(UiImageKind.CharacterPortrait, nativeId) : null).ToList();
            members = choices;
        }
        Place(body, Panel(null, members), 0);

        var right = Grid([3, 1], [1], 4);
        MenuCharacter[] reserves = Data.Characters.Where(character => !character.InParty).ToArray();
        Place(right, Panel(null, NativeSwitchMembers(reserves)), 0);
        MenuCharacter? selected = root ? Character : NativeSwitchCharacter(SelectedEntry);
        Place(right, Panel(null, Lines([selected?.Name ?? "--", $"Lv  {Number(selected?.Level)}", "HP  " + (selected is null ? "--" : Hp(selected))], 25)), 1);
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

    private UiElement NativeSwitchMembers(MenuCharacter[] characters)
    {
        bool reserves = characters.Length > 3;
        int columns = reserves ? 2 : 1;
        int rows = Math.Max(3, (characters.Length + columns - 1) / columns);
        var members = Grid(Enumerable.Repeat(1f, rows).ToArray(), Enumerable.Repeat(1f, columns).ToArray(), 5);
        for (int index = 0; index < characters.Length; index++)
        {
            MenuCharacter character = characters[index];
            var row = Grid([1], [1, 2.7f], 10);
            Place(row, Portrait(character), 0);
            Place(row, Lines([character.Name, $"Lv  {Number(character.Level)}", $"HP  {Hp(character)}"], reserves ? 18 : 24), 0, 1);
            Place(members, row, index / columns, index % columns);
        }
        return members;
    }
}
