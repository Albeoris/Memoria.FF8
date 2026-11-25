using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct BattleResistanceGrid(int Count, int Columns, float CellWidth, float IconSize, float FontSize, float TextHeight)
{
    public float Width => Columns * CellWidth;
    public float RowHeight => IconSize + TextHeight + 2;
    public float Height => Columns == 0 ? 0 : ((Count + Columns - 1) / Columns) * RowHeight;

    public static BattleResistanceGrid Measure(IUiCanvas canvas, float scale, float textScale, IReadOnlyList<BattleResistanceBadge> badges)
    {
        float iconSize = Math.Clamp(20 * textScale, 16, 28);
        float fontSize = Math.Clamp(13 * textScale, 11, 20);
        float width = iconSize;
        float textHeight = 0;
        for (int index = 0; index < badges.Count; index++)
        {
            BattleResistanceBadge badge = badges[index];
            UiSize value = canvas.MeasureText(badge.Value, fontSize * scale, float.PositiveInfinity);
            textHeight = Math.Max(textHeight, value.Height / scale);
            width = Math.Max(width, value.Width / scale);
            if (badge.Icon is null)
            {
                width = Math.Max(width, canvas.MeasureText(badge.Label, fontSize * scale, float.PositiveInfinity).Width / scale);
            }
        }
        return new(badges.Count, badges.Count, width + 6, iconSize, fontSize, textHeight);
    }

    public BattleResistanceGrid Fit(float width, float height)
    {
        if (Count == 0)
        {
            return this;
        }
        int rows = Math.Max(1, (int)(height / RowHeight));
        int columns = Math.Clamp((int)(width / CellWidth), (Count + rows - 1) / rows, Count);
        return this with { Columns = columns, CellWidth = Math.Min(width / columns, CellWidth) };
    }

    public BattleResistanceGrid Include(BattleResistanceGrid other)
    {
        int count = Math.Max(Count, other.Count);
        return other with { Count = count, Columns = count, CellWidth = Math.Max(CellWidth, other.CellWidth), TextHeight = Math.Max(TextHeight, other.TextHeight) };
    }
}

internal struct BattleTargetResistanceRow
{
    public const float Gap = 8;
    public IReadOnlyList<BattleResistanceBadge>? Badges;
    public BattleResistanceGrid Grid;
    public float NameWidth;
    public float NameHeight;
    public float BadgeX;
    public float BadgeY;

    public static float Arrange(BattleTargetResistanceRow[] rows, BattleResistanceGrid sharedGrid, float nameWidth, float availableWidth, float availableHeight)
    {
        bool inline = nameWidth + Gap + sharedGrid.Width <= availableWidth;
        float badgeX = inline ? nameWidth + Gap : 0;
        float badgeY = 0;
        if (!inline)
        {
            foreach (BattleTargetResistanceRow row in rows)
            {
                if (row.Grid.Count > 0)
                {
                    badgeY = Math.Max(badgeY, row.NameHeight + 2);
                }
            }
            sharedGrid = sharedGrid.Fit(availableWidth, availableHeight - badgeY);
        }
        for (int index = 0; index < rows.Length; index++)
        {
            ref BattleTargetResistanceRow row = ref rows[index];
            if (row.Grid.Count > 0)
            {
                row.NameWidth = inline ? nameWidth : availableWidth;
                row.BadgeX = badgeX;
                row.BadgeY = badgeY;
                row.Grid = sharedGrid with { Count = row.Grid.Count };
            }
        }
        return badgeY + sharedGrid.Height;
    }
}

internal sealed partial class BattleHudElement
{
    private BattleTargetResistanceRow[] resistanceRows = [];
    private readonly List<UiRect> modelHintBounds = [];

    private BattleTargetResistanceRow[]? MeasureResistanceRows(IUiCanvas canvas, IReadOnlyList<BattleCombatant> targets)
    {
        if (controller.ScanHintMode == BattleScanHintMode.Off)
        {
            return null;
        }
        if (resistanceRows.Length != targets.Count)
        {
            resistanceRows = new BattleTargetResistanceRow[targets.Count];
        }
        bool hasBadges = false;
        for (int index = 0; index < targets.Count; index++)
        {
            IReadOnlyList<BattleResistanceBadge> badges = controller.ResistanceBadges(targets[index]);
            resistanceRows[index] = default;
            if (badges.Count > 0)
            {
                resistanceRows[index].Badges = badges;
                resistanceRows[index].Grid = BattleResistanceGrid.Measure(canvas, Scale, controller.TextScale, badges);
                hasBadges = true;
            }
        }
        return hasBadges ? resistanceRows : null;
    }

    private void DrawModelResistanceHints(IUiCanvas canvas, UiTheme theme, UiRect spellMenu)
    {
        if (controller.TargetModelBounds is not { } project || Bounds.IsEmpty)
        {
            return;
        }
        modelHintBounds.Clear();
        modelHintBounds.Add(spellMenu);
        modelHintBounds.Add(drewExpandedParty ? Rect(partyList.Panel.X, partyList.Panel.Y, partyList.Panel.Width, partyList.Panel.Height) : Rect(BattleHudLayout.PartyLeft, BattleHudLayout.PartyTop, 438, 132));
        if (drewBattleMessage)
        {
            modelHintBounds.Add(Layout.MessagePanel);
        }
        float scale = Scale;
        float padding = 3 * scale;
        foreach (BattleCombatant target in controller.Session.Combatants)
        {
            if (target.IsPlayer || !target.IsActive || target.Hidden)
            {
                continue;
            }
            IReadOnlyList<BattleResistanceBadge> badges = controller.ResistanceBadges(target);
            if (badges.Count == 0 || project(target.Id) is not { } model || model.Intersect(Bounds).IsEmpty)
            {
                continue;
            }
            BattleResistanceGrid grid = BattleResistanceGrid.Measure(canvas, scale, controller.TextScale, badges).Fit(Math.Min(240, (Bounds.Width - 2 * padding) / scale), (Bounds.Height - 2 * padding) / scale);
            UiSize size = new(grid.Width * scale + 2 * padding, grid.Height * scale + 2 * padding);
            UiRect panel = PlaceModelHint(Bounds, model, size, 4 * scale, modelHintBounds);
            modelHintBounds.Add(panel);
            canvas.Fill(panel, new UiColor(20, 22, 28, 210));
            DrawResistanceBadges(canvas, theme, scale, grid, badges, panel.Inset(padding));
        }
    }

    internal static UiRect PlaceModelHint(UiRect viewport, UiRect model, UiSize size, float gap, IReadOnlyList<UiRect> placed)
    {
        float centerX = model.X + (model.Width - size.Width) / 2;
        float sideY = model.Y + (model.Height - size.Height) / 2;
        UiRect best = default;
        float leastOverlap = float.PositiveInfinity;
        if (TryPosition(centerX, model.Y - size.Height - gap) || TryPosition(model.Right + gap, sideY) ||
            TryPosition(model.X - size.Width - gap, sideY) || TryPosition(centerX, model.Bottom + gap))
        {
            return best;
        }
        foreach (UiRect other in placed)
        {
            if (TryPosition(centerX, other.Y - size.Height - gap) || TryPosition(centerX, other.Bottom + gap) ||
                TryPosition(other.Right + gap, sideY) || TryPosition(other.X - size.Width - gap, sideY))
            {
                break;
            }
        }
        return best;

        bool TryPosition(float x, float y)
        {
            UiRect panel = new(Math.Clamp(x, viewport.X, Math.Max(viewport.X, viewport.Right - size.Width)), Math.Clamp(y, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - size.Height)), size.Width, size.Height);
            float overlap = Area(panel.Intersect(model));
            for (int index = 0; index < placed.Count; index++)
            {
                overlap += Area(panel.Intersect(placed[index]));
            }
            if (overlap < leastOverlap)
            {
                leastOverlap = overlap;
                best = panel;
            }
            return overlap == 0;
        }

        static float Area(UiRect rect) => rect.Width * rect.Height;
    }

    internal static void DrawResistanceBadges(IUiCanvas canvas, UiTheme theme, float scale, BattleResistanceGrid grid, IReadOnlyList<BattleResistanceBadge> badges, UiRect bounds)
    {
        for (int index = 0; index < badges.Count; index++)
        {
            BattleResistanceBadge badge = badges[index];
            float x = bounds.X + index % grid.Columns * grid.CellWidth * scale;
            float y = bounds.Y + index / grid.Columns * grid.RowHeight * scale;
            UiRect header = new(x, y, grid.CellWidth * scale, grid.IconSize * scale);
            if (badge.Icon is { } icon)
            {
                float iconSize = Math.Min(header.Width, header.Height);
                canvas.Image(icon, new(header.X + (header.Width - iconSize) / 2, header.Y, iconSize, iconSize));
            }
            else
            {
                CenterText(badge.Label, header, theme.Foreground);
            }
            CenterText(badge.Value, new(x, header.Bottom, header.Width, grid.TextHeight * scale), badge.Tone.Color(theme));
        }

        void CenterText(string text, UiRect bounds, UiColor color)
        {
            float font = grid.FontSize * scale;
            UiSize measured = canvas.MeasureText(text, font, float.PositiveInfinity);
            float fit = Math.Min(1, Math.Min(bounds.Width / Math.Max(1, measured.Width), bounds.Height / Math.Max(1, measured.Height)));
            float inset = (bounds.Width - measured.Width * fit) / 2;
            canvas.Text(text, font * fit, bounds with { X = bounds.X + inset, Width = bounds.Width - inset }, color, UiAlignment.Center, UiTextFlow.SingleLine);
        }
    }
}
