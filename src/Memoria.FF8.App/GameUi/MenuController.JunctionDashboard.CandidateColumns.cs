using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiColumnPicker? junctionCandidateColumns;

    private UiElement BuildJunctionCandidateColumns(IReadOnlyList<JunctionCandidate> candidates)
    {
        bool magic = junctionEditor == "magic";
        UiGrid body = JunctionRows([4 * TextScale, 24 * TextScale, 0], [1], 2 * TextScale);
        UiElement description = magic ? BuildJunctionMagicDescription() : JunctionLabel(candidates.FirstOrDefault(candidate => candidate.Id == junctionCandidate)?.Detail ?? "", 14);
        description.Id = "junction-candidate-description";
        description.Margin = new UiThickness(8 * TextScale, 0, 8 * TextScale, 0);
        Place(body, description, 1);
        UiColumnPicker columns = new()
        {
            Id = magic ? "junction-magic-columns" : "junction-ability-columns",
            RowHeight = 26 * TextScale,
            NameQuantityGap = magic ? 8 * TextScale : 0,
            ImageSize = 18 * TextScale,
            ImageNameGap = 6 * TextScale,
            ReserveImageSpace = magic,
            ColumnGap = 16 * TextScale,
            HorizontalPadding = 8 * TextScale,
            FirstVisibleColumn = junctionCandidatePage,
            InitialIndex = Math.Max(0, candidates.ToList().FindIndex(candidate => "candidate-" + candidate.Id == junctionFocus)),
            FirstColumnChanged = first => junctionCandidatePage = first
        };
        junctionCandidateColumns = columns;
        Dictionary<string, int> stock = JunctionMagicInventory.Where(spell => spell.CharacterId == Character?.Id).ToDictionary(spell => spell.Id, spell => spell.Quantity);
        foreach (JunctionCandidate candidate in candidates)
        {
            UiButton row = JunctionButton("candidate-" + candidate.Id, "", () => ActivateJunctionCandidate(candidate.Id), candidate.Enabled);
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
            UiImageSource? source = magic ? JunctionCandidateMagic(candidate.Id)?.OwnerIcon : candidate.Icon;
            UiImage? icon = source is { } image ? new() { Source = image } : null;
            columns.Add(row, JunctionLabel(candidate.Name, 16), JunctionLabel(magic && stock.TryGetValue(candidate.Id, out int quantity) ? Number(quantity) : "", 16), icon);
        }
        Place(body, columns, 2);
        return body;
    }
}
