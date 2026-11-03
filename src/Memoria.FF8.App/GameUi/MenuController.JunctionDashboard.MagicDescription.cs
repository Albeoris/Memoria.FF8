using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private MenuMagic? JunctionCandidateMagic(string id) => JunctionMagicInventory.FirstOrDefault(spell => spell.CharacterId == Character?.Id && spell.Id == id);

    private UiElement BuildJunctionMagicDescription()
    {
        MenuMagic? spell = JunctionCandidateMagic(junctionCandidate);
        if (spell?.BoundSlot is not { Length: > 0 } slot || spell.OwnerCharacterId is not { } ownerId)
        {
            return JunctionLabel((Character?.Name ?? "") + " · " + StatText(_junctionSlot), 14);
        }

        MenuCharacter? before = (junctionPlan?.Before ?? Data.JunctionPlanner?.Snapshot ?? Data).Characters.FirstOrDefault(character => character.Id == ownerId);
        MenuCharacter? after = junctionPlan?.UnavailableReason is null ? junctionPlan?.After.Characters.FirstOrDefault(character => character.Id == ownerId) : null;
        UiGrid line = new() { Gap = 4 * TextScale };
        line.RowDefinitions.Add(UiGridLength.Star());
        AddIcon(spell.OwnerIcon);
        Add(JunctionLabel(spell.Name + ":", 14));
        AddIcon(new UiImageSource(UiImageKind.Icon, 192));
        Add(JunctionLabel(StatText(slot), 14));
        if (IsJunctionStat(slot))
        {
            if (before?.Stats.TryGetValue(slot, out int current) == true && after?.Stats.TryGetValue(slot, out int next) == true)
            {
                AddValue(current, next, slot is "Eva" or "Hit");
            }
        }
        else
        {
            bool elemental = slot.StartsWith("Elem-", StringComparison.Ordinal);
            bool attack = slot.EndsWith("-Atk", StringComparison.Ordinal);
            IReadOnlyList<MenuJunctionPercentage>? current = elemental ? before?.JunctionEffects?.Elements : before?.JunctionEffects?.Statuses;
            IReadOnlyList<MenuJunctionPercentage>? projected = elemental ? after?.JunctionEffects?.Elements : after?.JunctionEffects?.Statuses;
            bool separator = false;
            for (int index = 0; index < (current?.Count ?? 0); index++)
            {
                MenuJunctionPercentage value = current![index];
                MenuJunctionPercentage? next = projected?.ElementAtOrDefault(index);
                int oldValue = attack ? value.Attack : value.Defense;
                int? newValue = attack ? next?.Attack : next?.Defense;
                if (newValue is null || oldValue == newValue)
                {
                    continue;
                }
                if (separator)
                {
                    Add(JunctionLabel(",", 14));
                }
                AddIcon(new UiImageSource(UiImageKind.Icon, (elemental ? 288 : 272) + index));
                Add(JunctionLabel(value.Name, 14));
                AddValue(oldValue, newValue.Value, true);
                separator = true;
            }
        }
        line.ColumnDefinitions.Add(UiGridLength.Star());
        return line;

        void Add(UiElement element)
        {
            int column = line.ColumnDefinitions.Count;
            line.ColumnDefinitions.Add(UiGridLength.Auto);
            Place(line, element, 0, column);
        }

        void AddIcon(UiImageSource? source)
        {
            if (source is null)
            {
                return;
            }
            int column = line.ColumnDefinitions.Count;
            line.ColumnDefinitions.Add(UiGridLength.Pixel(18 * TextScale));
            Place(line, new UiImage { Source = source }, 0, column);
        }

        void AddValue(int current, int next, bool percentage)
        {
            string suffix = percentage ? "%" : "";
            UiLabel value = JunctionLabel(Number(current) + suffix + (current == next ? "" : " → " + Number(next) + suffix), 14);
            value.Color = next == current ? null : next > current ? JunctionIncreaseColor : JunctionDecreaseColor;
            Add(value);
        }
    }
}
