using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool IsEffectJunctionPage => ScreenId is "junction-element" or "junction-status" || ScreenId == "junction-stock" && (_junctionSlot.StartsWith("Elem-", StringComparison.Ordinal) || _junctionSlot.StartsWith("ST-", StringComparison.Ordinal));

    private UiElement BuildEffectJunction(bool vector)
    {
        bool choosingSpell = ScreenId == "junction-stock";
        bool elemental = ScreenId == "junction-element" || choosingSpell && _junctionSlot.StartsWith("Elem-", StringComparison.Ordinal);
        string prefix = elemental ? "Elem" : "ST";
        string[] slots = [$"{prefix}-Atk", $"{prefix}-Def 1", $"{prefix}-Def 2", $"{prefix}-Def 3", $"{prefix}-Def 4"];
        var body = Grid([0.95f, 1.05f], [1, 1.65f], 8);
        body.Id = "junction-effects";
        var identity = Grid([1], [0.7f, 1.3f], 8);
        Place(identity, Portrait(Character), 0);
        Place(identity, Lines([Character?.Name ?? "--", $"Lv  {Number(Character?.Level)}", "HP  " + (Character is null ? "--" : Hp(Character))], 23), 0, 1);
        Place(body, EffectPanel(null, identity), 0);
        Place(body, EffectPanel("MAGIC STOCK", choosingSpell ? Commands(27, 20) : MagicStockPreview(Character)), 0, 1);

        var junctions = Grid([0.65f, 5], [1], 4);
        Place(junctions, JunctionPageLinks(elemental), 0);
        UiElement choices;
        if (!choosingSpell)
        {
            choices = Commands(30, 21);
        }
        else
        {
            var rows = Grid([1, 1, 1, 1, 1], [1], 0);
            for (int index = 0; index < slots.Length; index++)
            {
                string slot = slots[index];
                var row = Grid([1], [1, 1.3f], 4);
                Place(row, JunctionSlotLabel(slot, (slot == _junctionSlot ? "› " : "") + slot, 19), 0);
                string magic = Character?.Junctions.GetValueOrDefault(slot, "----") ?? "----";
                if (slot == _junctionSlot && SelectedEntry is { Enabled: true, Action.Kind: "junction-magic" or "junction-magic-reassign" })
                {
                    magic = SelectedEntry.Action.SubjectId == "0" ? "----" : CommandMagic(SelectedEntry)?.Name ?? magic;
                }
                Place(row, JunctionSlotLabel(slot, magic, 19), 0, 1);
                Place(rows, row, index);
            }
            choices = rows;
        }
        Place(junctions, choices, 1);
        Place(body, EffectPanel(elemental ? "EL-A/D" : "ST-A/D", junctions), 1);

        MenuJunctionEffects? current = Character?.JunctionEffects;
        MenuJunctionEffects? preview = choosingSpell && SelectedEntry is { Enabled: true, Action.Kind: "junction-magic" or "junction-magic-reassign" } entry ? Character?.JunctionEffectsPreview?.Invoke(_junctionSlot, entry.Action.SubjectId) : null;
        IReadOnlyList<MenuJunctionPercentage>? values = elemental ? current?.Elements : current?.Statuses;
        IReadOnlyList<MenuJunctionPercentage>? projected = elemental ? preview?.Elements : preview?.Statuses;
        string[] names = elemental ? ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"] : ["Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain"];
        int rowCount = (names.Length + 1) / 2;
        var percentages = Grid([0.65f, rowCount], [1], 4);
        Place(percentages, Label(elemental ? "Attack / Defense    >100% defense: absorb" : "Attack / Defense    Current → Preview", 16), 0);
        var columns = Grid([1], [1, 1], 12);
        for (int column = 0; column < 2; column++)
        {
            var rows = Grid(Enumerable.Repeat(1f, rowCount).ToArray(), [1], 0);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                int index = column * rowCount + rowIndex;
                if (index >= names.Length)
                {
                    break;
                }
                string name = values?.ElementAtOrDefault(index)?.Name ?? names[index];
                MenuJunctionPercentage? value = values?.ElementAtOrDefault(index);
                MenuJunctionPercentage? next = projected?.ElementAtOrDefault(index);
                var row = Grid([1], [0.85f, 1.4f], 3);
                Place(row, Label(name, 17), 0);
                var numbers = Grid([1], [1, 1], 5);
                Place(numbers, Percentage("attack", value?.Attack, next?.Attack), 0);
                Place(numbers, Percentage("defense", value?.Defense, next?.Defense), 0, 1);
                Place(row, numbers, 0, 1);
                Place(rows, row, rowIndex);

                UiLabel Percentage(string kind, int? before, int? after)
                {
                    string text = before.HasValue ? before + "%" : "--";
                    if (after.HasValue && after != before)
                    {
                        text = before + " → " + after + "%";
                    }
                    UiLabel label = Label(text, 16);
                    label.Id = $"junction-{prefix}-{kind}-{index}";
                    if (after.HasValue && before.HasValue && after != before)
                    {
                        label.Color = after > before ? new UiColor(120, 190, 255) : new UiColor(255, 120, 120);
                    }
                    return label;
                }
            }
            Place(columns, rows, 0, column);
        }
        Place(percentages, columns, 1);
        Place(body, EffectPanel(elemental ? "ELEMENTAL ATTACK / DEFENSE" : "STATUS ATTACK / DEFENSE", percentages), 1, 1);
        return body;

        UiPanel EffectPanel(string? caption, UiElement content) => vector ? VectorPanel(caption, content, 10) : Panel(caption, content);
    }

    private UiElement JunctionAbilityPageLink() => new UiButton { Text = "‹ EL-A/D", FontSize = 17 * TextScale, DrawBackground = false, PointerSize = 0, Click = () => Navigate(MenuInput.Left) };

    private UiElement JunctionPageLinks(bool elemental)
    {
        var links = Grid([1], [1, 1], 3);
        if (elemental)
        {
            Place(links, Link("‹ ST-A/D", MenuInput.Left), 0);
            Place(links, Link("Ability ›", MenuInput.Right), 0, 1);
        }
        else
        {
            Place(links, Link("EL-A/D ›", MenuInput.Right), 0, 1);
        }
        return links;

        UiButton Link(string text, MenuInput input) => new() { Text = text, FontSize = 17 * TextScale, DrawBackground = false, PointerSize = 0, Enabled = ScreenId != "junction-stock", Click = () => Navigate(input) };
    }
}
