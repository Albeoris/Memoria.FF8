using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiColumnPicker? junctionMagicColumns;

    private UiElement BuildJunctionMagicColumns(IReadOnlyList<JunctionCandidate> candidates)
    {
        UiGrid body = JunctionRows([22, 0], [1], 2);
        Place(body, JunctionLabel((Character?.Name ?? "") + " · " + StatText(_junctionSlot), 14), 0);
        UiColumnPicker columns = new()
        {
            Id = "junction-magic-columns",
            RowHeight = 26 * TextScale,
            NameQuantityGap = 8 * TextScale,
            ColumnGap = 16 * TextScale,
            FirstVisibleColumn = junctionMagicFirstColumn,
            InitialIndex = Math.Max(0, candidates.ToList().FindIndex(candidate => "candidate-" + candidate.Id == junctionFocus)),
            FirstColumnChanged = first => junctionMagicFirstColumn = first
        };
        junctionMagicColumns = columns;
        Dictionary<string, int> stock = JunctionMagicInventory.Where(spell => spell.CharacterId == Character?.Id).ToDictionary(spell => spell.Id, spell => spell.Quantity);
        foreach (JunctionCandidate candidate in candidates)
        {
            UiButton row = JunctionButton("candidate-" + candidate.Id, "", () => ActivateJunctionCandidate(candidate.Id));
            row.DrawBackground = false;
            row.Hover = () =>
            {
                junctionFocus = row.Id!;
                PreviewJunctionCandidate(candidate.Id);
            };
            row.FocusChanged = focused =>
            {
                if (focused && !junctionRestoringFocus)
                {
                    junctionFocus = row.Id!;
                    if (junctionCandidate != candidate.Id)
                    {
                        PreviewJunctionCandidate(candidate.Id);
                    }
                }
            };
            columns.Add(row, JunctionLabel(candidate.Name, 16), JunctionLabel(stock.TryGetValue(candidate.Id, out int quantity) ? Number(quantity) : "", 16));
        }
        Place(body, columns, 1);
        return body;
    }
}
