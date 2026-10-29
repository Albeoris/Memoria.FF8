using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class TripleTriadElementView
{
    private static readonly UiRect RedrawBounds = new(746, 0, 196, 24);
    private static readonly UiRect StartBounds = new(24, 0, 196, 24);
    private string? hintPosition;
    private readonly Dictionary<int, (bool Same, bool Plus)> handHints = [];

    public override void PointerHover(float x, float y)
    {
        if (controller.Match is not { } match || !controller.ChoosingCell)
        {
            return;
        }
        for (int cell = 0; cell < match.Board.Count; cell++)
        {
            if (CellBounds(cell).Contains(x, y) && !match.IsBlocked(cell))
            {
                controller.HoverCell(cell);
                return;
            }
        }
    }

    private void DrawRedraw(IUiCanvas canvas, UiTheme theme)
    {
        if (controller.CanRedraw)
        {
            theme.Panel(canvas, RedrawBounds);
            canvas.Text(controller.FormatSystemText("card.ui.redraw", "Redraw ({0})", controller.RedrawsRemaining), 19, RedrawBounds.Inset(4), theme.Foreground, UiAlignment.Center);
        }
        if (controller.AwaitingOpeningConfirmation)
        {
            theme.Panel(canvas, StartBounds);
            canvas.Text(controller.SystemText("card.ui.start", "Begin match"), 19, StartBounds.Inset(4), theme.Foreground, UiAlignment.Center);
        }
        if (controller.OpeningControlSelected)
        {
            UiRect selected = controller.RedrawSelected ? RedrawBounds : StartBounds;
            theme.Pointer(canvas, selected.X + 4, selected.Bottom, 20);
        }
    }

    private void DrawAssistance(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match)
    {
        TripleTriadAssistance assistance = controller.Options.Assistance;
        if (assistance == TripleTriadAssistance.Off || match.CurrentPlayer != 0 || match.Outcome != TripleTriadOutcome.InProgress || controller.IsAnimating || controller.AwaitingReplay)
        {
            return;
        }
        if (assistance >= TripleTriadAssistance.Hand)
        {
            DrawHandHints(canvas, match);
        }
        int cell = controller.SelectedCell;
        if (!controller.ChoosingCell || match.IsBlocked(cell) || match.Board[cell] is not null || match.Hands[0][controller.SelectedHand] is not int id)
        {
            return;
        }
        UiRect destination = CellBounds(cell);
        int modifier = match.GetDisplayElementModifier(id, cell);
        canvas.ImageStretched(new UiImageSource(UiImageKind.CardFace, id, 0, modifier, 135), destination);
        DrawElementModifier(canvas, theme, match, id, cell, destination);
        IReadOnlyList<TripleTriadCapture> captures;
        TripleTriadMoveResult? preview = null;
        if (assistance >= TripleTriadAssistance.Rules)
        {
            preview = match.PreviewMove(controller.SelectedHand, cell);
            captures = preview.Captures.Where(capture => assistance >= TripleTriadAssistance.Combo || capture.Wave == 0).ToArray();
        }
        else
        {
            captures = DirectCaptures(match, id, cell);
        }
        float pulse = (1 + MathF.Sin((float)controller.PresentationSeconds * 2.2f)) / 2;
        foreach (TripleTriadCapture capture in captures)
        {
            TripleTriadPlacedCard placed = match.Board[capture.CellIndex]!;
            UiRect target = CellBounds(capture.CellIndex);
            canvas.ImageStretched(new UiImageSource(UiImageKind.CardFace, placed.CardId, 0, match.GetDisplayElementModifier(placed.CardId, capture.CellIndex), (byte)(pulse * 210)), target);
            DrawArrow(canvas, CellBounds(capture.SourceCellIndex), target, capture.Wave > 0);
        }
        if (preview is { } && (preview.Same || preview.Plus))
        {
            DrawRuleReason(canvas, match, id, cell, preview, pulse);
        }
    }

    internal static IReadOnlyList<TripleTriadCapture> DirectCaptures(TripleTriadMatch match, int cardId, int source)
    {
        List<TripleTriadCapture> result = [];
        for (int direction = 0; direction < 4; direction++)
        {
            int adjacent = Neighbor(match, source, direction);
            if (adjacent >= 0 && match.Board[adjacent] is { Owner: 1 } placed && match.GetEffectiveRank(cardId, source, direction) > match.GetEffectiveRank(placed.CardId, adjacent, (direction + 2) % 4))
            {
                result.Add(new TripleTriadCapture(adjacent, source, 0));
            }
        }
        return result;
    }

    private static int Neighbor(TripleTriadMatch match, int cell, int direction) => direction switch
    {
        0 => cell >= match.Width ? cell - match.Width : -1,
        1 => cell % match.Width < match.Width - 1 ? cell + 1 : -1,
        2 => cell + match.Width < match.Board.Count ? cell + match.Width : -1,
        _ => cell % match.Width > 0 ? cell - 1 : -1
    };

    private static void DrawArrow(IUiCanvas canvas, UiRect source, UiRect target, bool combo)
    {
        float x = source.X + source.Width / 2;
        float y = source.Y + source.Height / 2;
        float dx = target.X + target.Width / 2 - x;
        float dy = target.Y + target.Height / 2 - y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float ux = dx / distance;
        float uy = dy / distance;
        float startX = x + dx * 0.25f;
        float startY = y + dy * 0.25f;
        float endX = x + dx * 0.78f;
        float endY = y + dy * 0.78f;
        float size = Math.Min(source.Width, target.Width) * 0.10f;
        foreach ((UiColor color, float thickness) in new[] { (new UiColor(20, 36, 75, 235), 7f), (new UiColor(164, 210, 255, 245), 4f), (Blue, 2f) })
        {
            canvas.Line(startX, startY, endX, endY, color, thickness);
            canvas.Line(endX, endY, endX - ux * size - uy * size * 0.65f, endY - uy * size + ux * size * 0.65f, color, thickness);
            canvas.Line(endX, endY, endX - ux * size + uy * size * 0.65f, endY - uy * size - ux * size * 0.65f, color, thickness);
        }
        if (combo)
        {
            canvas.Image(new UiImageSource(UiImageKind.TripleTriadCapture, 2), new UiRect((startX + endX) / 2 - 32, (startY + endY) / 2 - 14, 64, 16));
        }
    }

    private void DrawHandHints(IUiCanvas canvas, TripleTriadMatch match)
    {
        string position = string.Join(',', match.BlockedCells) + "/" + match.UseModifiedSamePlus + "/" + match.Rules + "/" + string.Join(',', match.BoardElements) + "/" + string.Join(',', match.Board.Select(placed => placed is null ? "-" : $"{placed.CardId}:{placed.Owner}")) + "/" + string.Join(',', match.Hands[0]);
        if (position != hintPosition)
        {
            hintPosition = position;
            handHints.Clear();
            for (int hand = 0; hand < match.HandSize; hand++)
            {
                if (match.Hands[0][hand] is null)
                {
                    continue;
                }
                bool same = false;
                bool plus = false;
                for (int cell = 0; cell < match.Board.Count; cell++)
                {
                    if (match.Board[cell] is null && !match.IsBlocked(cell))
                    {
                        TripleTriadMoveResult preview = match.PreviewMove(hand, cell);
                        same |= preview.Same;
                        plus |= preview.Plus;
                    }
                }
                handHints[hand] = (same, plus);
            }
        }
        foreach ((int hand, (bool same, bool plus)) in handHints)
        {
            if (!same && !plus)
            {
                continue;
            }
            UiRect bounds = HandBounds(hand, 0);
            string text = same && plus ? "Same / Plus" : same ? "Same" : "Plus";
            UiRect badge = new(bounds.X + bounds.Width * 0.36f, bounds.Y + 3, bounds.Width * 0.64f, Math.Min(25, bounds.Height / 4));
            canvas.Fill(badge, new UiColor(27, 41, 79, 220));
            canvas.OutlinedText(text, Math.Min(17, bounds.Width / 8), badge, Gold, new UiColor(20, 20, 35), 0.7f);
        }
    }

    private void DrawRuleReason(IUiCanvas canvas, TripleTriadMatch match, int cardId, int cell, TripleTriadMoveResult preview, float pulse)
    {
        List<string> reason = [];
        foreach ((bool same, IReadOnlyList<TripleTriadRuleParticipant> participants) in new[] { (true, preview.SameParticipants), (false, preview.PlusParticipants) })
        {
            foreach (TripleTriadRuleParticipant pair in participants)
            {
                GlowRank(canvas, CellBounds(cell), pair.Direction, RankText(pair.OwnRank), pulse, match.GetDisplayElementModifier(cardId, cell));
                if (!pair.IsWall)
                {
                    TripleTriadPlacedCard adjacent = match.Board[pair.CellIndex]!;
                    GlowRank(canvas, CellBounds(pair.CellIndex), (pair.Direction + 2) % 4, RankText(pair.AdjacentRank), pulse, match.GetDisplayElementModifier(adjacent.CardId, pair.CellIndex));
                }
                if (pair.IsWall)
                {
                    DrawWallHint(canvas, cell, pair.Direction, pair.CellIndex, pulse);
                }
                string other = pair.IsWall ? "Wall A" : RankText(pair.AdjacentRank);
                reason.Add(same ? $"{RankText(pair.OwnRank)}={other}" : $"{RankText(pair.OwnRank)}+{other}={pair.OwnRank + pair.AdjacentRank}");
            }
        }
        string label = (preview.Same ? "Same" : "") + (preview.Same && preview.Plus ? " / " : "") + (preview.Plus ? "Plus" : "");
        canvas.OutlinedText(label + ": " + string.Join(" · ", reason.Distinct()), 17, new UiRect(210, 648, 540, 26), new UiColor(255, 240, 191), Ink, 1);
    }

    private static void DrawElementModifier(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match, int cardId, int cell, UiRect bounds)
    {
        int modifier = match.GetElementModifier(cardId, cell);
        if (modifier == 0)
        {
            return;
        }
        int displayModifier = match.GetDisplayElementModifier(cardId, cell);
        UiColor color = displayModifier == 0 ? theme.Foreground : modifier > 0 ? new UiColor(90, 235, 105) : new UiColor(245, 75, 65);
        float scale = Math.Min(1, bounds.Width / 160);
        canvas.OutlinedText(modifier > 0 ? "+1" : "−1", 23 * scale, new UiRect(bounds.Right - 36 * scale, bounds.Bottom - 31 * scale, 35 * scale, 27 * scale), color, new UiColor(23, 18, 14), 1.4f * scale);
    }

    private static string RankText(int rank) => CardFaceComposer.RankText(rank);

    private static void GlowRank(IUiCanvas canvas, UiRect bounds, int direction, string rank, float pulse, int modifier)
    {
        (float x, float y) = direction switch { 0 => (6, 2), 1 => (10, 10), 2 => (6, 18), _ => (2, 10) };
        UiRect digit = new(bounds.X + x / 64 * bounds.Width, bounds.Y + y / 64 * bounds.Height, bounds.Width * 12 / 64, bounds.Height * 12 / 64);
        UiColor color = modifier switch
        {
            > 0 => new UiColor((byte)(60 + 80 * pulse), 255, (byte)(90 + 60 * pulse)),
            < 0 => new UiColor(255, (byte)(60 + 80 * pulse), (byte)(50 + 70 * pulse)),
            _ => new UiColor((byte)(140 + 115 * pulse), (byte)(205 + 40 * pulse), 255)
        };
        canvas.OutlinedText(rank, bounds.Height * 9 / 64, digit, color, new UiColor(37, 58, 112), 1 + pulse);
    }
}
