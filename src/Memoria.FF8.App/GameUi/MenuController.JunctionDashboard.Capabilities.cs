using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiElement BuildJunctionGuardianCapabilities()
    {
        UiGrid groups = Grid([1], [1, 1], 12);
        Place(groups, Group(true), 0);
        Place(groups, Group(false), 0, 1);
        return groups;

        UiElement Group(bool commands)
        {
            IReadOnlyList<MenuAbilityChoice> current = commands ? Character?.AvailableCommands ?? [] : Character?.AvailableAbilities ?? [];
            MenuCharacter? preview = JunctionPreviewCharacter;
            IReadOnlyList<MenuAbilityChoice> next = preview is null ? current : commands ? preview.AvailableCommands : preview.AvailableAbilities;
            HashSet<string> before = current.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
            HashSet<string> after = next.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
            MenuAbilityChoice[] choices = next.Concat(current).DistinctBy(choice => choice.Id).ToArray();
            string id = commands ? "gf-available-commands" : "gf-available-passives";
            UiGrid group = JunctionRows([24, 0], [1]);
            Place(group, JunctionLabel(commands ? J("available-commands", "Available command abilities") : J("available-passives", "Available passive abilities"), 15, true), 0);
            if (choices.Length == 0)
            {
                Place(group, JunctionLabel("---", 16, true), 1);
            }
            else
            {
                UiList list = new()
                {
                    Id = id,
                    Items = choices.Select(choice => (before.Contains(choice.Id) ? after.Contains(choice.Id) ? "" : "− " : "+ ") + choice.Name).ToList(),
                    Images = choices.Select(choice => choice.Icon).ToList(),
                    FontSize = 15 * TextScale,
                    RowHeight = 23 * TextScale,
                    AutoRowHeight = false,
                    TextInset = 30,
                    Paginated = true,
                    ShowSelection = true,
                    ItemTextColor = index => !before.Contains(choices[index].Id) ? JunctionIncreaseColor : !after.Contains(choices[index].Id) ? JunctionDecreaseColor : null,
                    FocusChanged = focused =>
                    {
                        if (focused)
                        {
                            junctionFocus = id;
                        }
                    }
                };
                Place(group, list, 1);
            }
            return group;
        }
    }
}
