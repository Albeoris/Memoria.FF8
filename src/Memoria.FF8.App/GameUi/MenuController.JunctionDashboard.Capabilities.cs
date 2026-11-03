using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiColumnPicker? junctionGuardianColumns;

    private UiElement BuildJunctionGuardianCapabilities(MenuCharacter? donor = null)
    {
        if (donor is null)
        {
            junctionGuardianColumns = null;
        }
        UiGrid body = JunctionRows([22 * TextScale, 0], [1], 2);
        Place(body, JunctionLabel(J("available-abilities", "Available abilities"), 14, true), 0);
        MenuCharacter? currentCharacter = donor ?? Character;
        MenuCharacter? preview = donor is null ? JunctionPreviewCharacter : junctionPlan?.After.Characters.FirstOrDefault(actor => actor.Id == donor.Id);
        var changes = Capabilities(true).Concat(Capabilities(false)).Concat(Capacity()).Where(change => donor is null || change.Delta < 0).OrderBy(change => change.Delta == 0 ? 1 : 0).ToArray();
        if (changes.Length == 0)
        {
            Place(body, JunctionLabel("---", 16, true), 1);
            return body;
        }
        UiColumnPicker columns = new()
        {
            Id = donor is null ? "gf-capability-columns" : "gf-transfer-loss-columns",
            RowHeight = 23 * TextScale,
            HorizontalPadding = 8 * TextScale,
            ColumnGap = 16 * TextScale,
            NameQuantityGap = 0,
            ImageSize = 18 * TextScale,
            ImageNameGap = 6 * TextScale,
            InitialIndex = Math.Max(0, Array.FindIndex(changes, change => (donor is null ? change.Id : "transfer-loss-" + change.Id) == junctionFocus))
        };
        if (donor is null)
        {
            junctionGuardianColumns = columns;
        }
        else
        {
            junctionTransferColumns = columns;
        }
        foreach (var change in changes)
        {
            UiButton row = new()
            {
                Id = donor is null ? change.Id : "transfer-loss-" + change.Id,
                DrawBackground = false,
                PointerSize = 0,
                FocusChanged = focused =>
                {
                    if (focused)
                    {
                        junctionFocus = donor is null ? change.Id : "transfer-loss-" + change.Id;
                    }
                }
            };
            UiLabel name = JunctionLabel((change.Delta > 0 ? "+ " : change.Delta < 0 ? "− " : "") + change.Choice.Name, 15);
            name.Color = change.Delta > 0 ? JunctionIncreaseColor : change.Delta < 0 ? JunctionDecreaseColor : null;
            UiImage? icon = change.Choice.Icon is { } image ? new() { Source = image } : null;
            columns.Add(row, name, JunctionLabel("", 15), icon);
        }
        Place(body, columns, 1);
        return body;

        IEnumerable<(string Id, MenuAbilityChoice Choice, int Delta)> Capacity()
        {
            if (currentCharacter is not null && preview is not null && currentCharacter.AbilitySlots != preview.AbilitySlots)
            {
                string name = J("passives", "Passive abilities") + $" · {currentCharacter.AbilitySlots} → {preview.AbilitySlots}";
                yield return ("gf-capability-capacity", new MenuAbilityChoice("capacity", name), Math.Sign(preview.AbilitySlots - currentCharacter.AbilitySlots));
            }
        }

        IEnumerable<(string Id, MenuAbilityChoice Choice, int Delta)> Capabilities(bool commands)
        {
            IReadOnlyList<MenuAbilityChoice> current = commands ? currentCharacter?.AvailableCommands ?? [] : currentCharacter?.AvailableAbilities ?? [];
            IReadOnlyList<MenuAbilityChoice> next = preview is null ? current : commands ? preview.AvailableCommands : preview.AvailableAbilities;
            HashSet<string> before = current.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
            HashSet<string> after = next.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
            foreach (MenuAbilityChoice choice in next.Concat(current).DistinctBy(choice => choice.Id))
            {
                yield return ("gf-capability-" + (commands ? "command-" : "passive-") + choice.Id, choice, !before.Contains(choice.Id) ? 1 : !after.Contains(choice.Id) ? -1 : 0);
            }
        }
    }
}
