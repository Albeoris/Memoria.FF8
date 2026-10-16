using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class TripleTriadElementView(TripleTriadController controller) : UiElement
{
    private int scroll;
    private static readonly UiColor Blue = new(40, 100, 180);
    private static readonly UiColor Red = new(174, 56, 52);

    public override void Arrange(UiRect bounds)
    {
        Focusable = true;
        base.Arrange(bounds);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Fill(Bounds, new UiColor(12, 19, 30));
        theme.Panel(canvas, new UiRect(16, 14, 928, 54));
        canvas.Text("TRIPLE TRIAD", 27, new UiRect(32, 23, 370, 38), theme.Foreground);
        canvas.Text(controller.Match is { } match ? $"YOU  {controller.PresentedScore(0)}  :  {controller.PresentedScore(1)}  OPPONENT" : $"Choose your hand   {controller.Deck.Count} / 5", 20, new UiRect(450, 28, 470, 30), theme.Foreground);
        if (controller.AwaitingRules)
        {
            theme.Panel(canvas, new UiRect(180, 106, 600, 412));
            canvas.Text("RULES", 28, new UiRect(210, 133, 540, 50), theme.Foreground);
            canvas.Text(controller.Rules == TripleTriadRules.None ? "Basic rules" : controller.Rules.ToString(), 23, new UiRect(210, 209, 540, 140), theme.Foreground);
            canvas.Text("Trade rule: " + controller.TradeRuleDescription, 23, new UiRect(210, 366, 540, 50), theme.Foreground);
            canvas.Text("Play       Enter / Confirm\nQuit        Esc / Cancel", 19, new UiRect(210, 438, 540, 68), theme.Foreground);
        }
        else if (controller.TradingHand is not null)
        {
            DrawTrade(canvas, theme);
        }
        else if (controller.Match is null)
        {
            DrawSelection(canvas, theme);
        }
        else
        {
            DrawMatch(canvas, theme, controller.Match);
        }
        theme.Panel(canvas, new UiRect(16, 566, 928, 90));
        canvas.Text(controller.IsAnimating ? controller.CaptureAnnouncement ?? "" : controller.Message, 20, new UiRect(32, 578, 896, 32), theme.Foreground);
        canvas.Text("↑ ↓ ← → Move     Enter Confirm     Esc Back     F5 Save     F9 Load", 16, new UiRect(32, 621, 896, 27), theme.Muted);
        if (controller.PromptFade > 0)
        {
            canvas.Fill(Bounds, new UiColor(0, 0, 0, (byte)(Math.Clamp(controller.PromptFade, 0, 1) * 255)));
        }
    }

    private void DrawTrade(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Text($"Choose your winnings   {controller.TradeSelection.Count} / {controller.RequiredTradeCards}", 24, new UiRect(32, 92, 890, 45), theme.Foreground);
        for (int slot = 0; slot < 5; slot++)
        {
            int id = controller.TradingHand![slot];
            UiRect bounds = new(40 + slot * 182, 180, 154, 154);
            DrawCard(canvas, theme, bounds, id, controller.TradeSelection.Contains(slot) ? 0 : 1, controller.Selection == slot);
            canvas.Text(controller.Name(id), 18, new UiRect(bounds.X, 352, 154, 80), theme.Foreground);
            if (controller.TradeSelection.Contains(slot))
            {
                canvas.Text("Selected", 17, new UiRect(bounds.X, 450, 154, 34), Blue);
            }
        }
    }

    private void DrawSelection(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, new UiRect(16, 80, 422, 474));
        scroll = Math.Clamp(scroll, Math.Max(0, controller.Selection - 11), controller.Selection);
        for (int row = 0; row < 12 && row + scroll < controller.Available.Count; row++)
        {
            int index = row + scroll;
            int id = controller.Available[index];
            UiRect bounds = new(50, 94 + row * 37, 372, 35);
            if (index == controller.Selection)
            {
                canvas.Fill(bounds, new UiColor(130, 130, 130, 120));
                theme.Pointer(canvas, 24, bounds.Y + 10, 14);
            }
            canvas.Text(controller.Name(id), 19, new UiRect(bounds.X + 8, bounds.Y + 6, 292, 27), controller.Remaining(id) > 0 ? theme.Foreground : theme.Muted);
            canvas.Text($"×{controller.Remaining(id)}", 18, new UiRect(366, bounds.Y + 6, 50, 27), theme.Foreground);
        }
        int selected = controller.Available[controller.Selection];
        DrawCard(canvas, theme, new UiRect(590, 100, 240, 240), selected, 0, false);
        canvas.OutlinedText(controller.Name(selected), 26, new UiRect(482, 350, 440, 38), theme.Foreground, new UiColor(0, 0, 0), 1);
        canvas.Text($"Level {selected / 11 + 1}     {controller.Deck.Count} / 5 cards selected", 17, new UiRect(482, 395, 440, 28), theme.Muted);
        for (int slot = 0; slot < 5; slot++)
        {
            UiRect bounds = new(466 + slot * 94, 451, 84, 84);
            if (slot < controller.Deck.Count)
            {
                DrawCard(canvas, theme, bounds, controller.Deck[slot], 0, false);
            }
            else
            {
                canvas.Fill(bounds, new UiColor(20, 25, 38, 190));
                canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y, new UiColor(108, 99, 82), 1);
                canvas.Text((slot + 1).ToString(), 22, bounds.Inset(27), theme.Muted);
            }
        }
    }

    private void DrawMatch(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match)
    {
        UiRect board = new(264, 82, 432, 432);
        canvas.Fill(board, new UiColor(36, 30, 24, 226));
        canvas.Line(board.X, board.Y, board.Right, board.Y, new UiColor(171, 151, 111), 2);
        canvas.Line(board.X, board.Y, board.X, board.Bottom, new UiColor(171, 151, 111), 2);
        canvas.Line(board.X, board.Bottom - 2, board.Right, board.Bottom - 2, new UiColor(89, 73, 53), 2);
        canvas.Line(board.Right - 2, board.Y, board.Right - 2, board.Bottom, new UiColor(89, 73, 53), 2);
        for (int cell = 0; cell < 9; cell++)
        {
            UiRect square = CellBounds(cell);
            canvas.Fill(square, new UiColor(73, 65, 53, 155));
            canvas.Line(square.X, square.Y, square.Right, square.Y, new UiColor(144, 130, 99), 1);
            canvas.Line(square.X, square.Y, square.X, square.Bottom, new UiColor(144, 130, 99), 1);
        }
        IReadOnlyList<IReadOnlyList<int?>> hands = match.Hands;
        IReadOnlyList<TripleTriadPlacedCard?> cards = match.Board;
        IReadOnlyList<TripleTriadElement> elements = match.BoardElements;
        for (int owner = 0; owner < 2; owner++)
        {
            canvas.OutlinedText(owner == 0 ? "YOU" : "OPPONENT", 19, new UiRect(owner == 0 ? 46 : 760, 82, 170, 30), owner == 0 ? Blue : Red, new UiColor(0, 0, 0), 1);
            for (int hand = 0; hand < 5; hand++)
            {
                if (hands[owner][hand] is int id)
                {
                    bool selected = owner == 0 && !controller.ChoosingCell && controller.SelectedHand == hand && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating;
                    DrawCard(canvas, theme, HandBounds(hand, owner), owner == 0 || match.Rules.HasFlag(TripleTriadRules.Open) ? id : null, owner, selected);
                }
            }
        }
        for (int cell = 0; cell < 9; cell++)
        {
            UiRect bounds = CellBounds(cell);
            bool selected = controller.ChoosingCell && controller.SelectedCell == cell && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating;
            if (cards[cell] is { } placed)
            {
                if (controller.PresentedMove is { } move && move.CellIndex == cell && controller.PlacementProgress < 1)
                {
                    continue;
                }
                (int owner, float width, float height) = controller.PresentedCard(cell, placed.Owner);
                UiRect turned = new(bounds.X + bounds.Width * (1 - width) / 2, bounds.Y + bounds.Height * (1 - height) / 2, bounds.Width * width, bounds.Height * height);
                DrawCard(canvas, theme, turned, placed.CardId, owner, false);
                if (match.Rules.HasFlag(TripleTriadRules.Elemental) && elements[cell] != TripleTriadElement.None && width > 0.8 && height > 0.8)
                {
                    string modifier = controller.Card(placed.CardId).Element == elements[cell] ? "+1" : "−1";
                    canvas.OutlinedText(modifier, 23, new UiRect(bounds.Right - 36, bounds.Bottom - 31, 35, 27), theme.Foreground, new UiColor(23, 18, 14), 1.4f);
                }
            }
            else
            {
                if (elements[cell] != TripleTriadElement.None)
                {
                    canvas.Image(new UiImageSource(UiImageKind.CardElement, (int)elements[cell]), new UiRect(bounds.X + 48, bounds.Y + 48, 38, 38));
                }
                if (selected)
                {
                    canvas.Fill(bounds, new UiColor(255, 239, 179, 42));
                    theme.Pointer(canvas, bounds.X + 42, bounds.Y + 56, 28);
                }
            }
        }
        if (controller.PresentedMove is { } moving && controller.PlacementProgress < 1)
        {
            UiRect source = HandBounds(moving.HandIndex, moving.Player);
            UiRect destination = CellBounds(moving.CellIndex);
            float progress = (float)controller.PlacementProgress;
            float eased = 1 - MathF.Pow(1 - progress, 3);
            UiRect bounds = new(source.X + (destination.X - source.X) * eased, source.Y + (destination.Y - source.Y) * eased, source.Width + (destination.Width - source.Width) * eased, source.Height + (destination.Height - source.Height) * eased);
            bool reveal = moving.Player == 1 && !match.Rules.HasFlag(TripleTriadRules.Open);
            if (reveal)
            {
                float width = Math.Max(0.015f, MathF.Abs(MathF.Cos(progress * MathF.PI)));
                bounds = new UiRect(bounds.X + bounds.Width * (1 - width) / 2, bounds.Y, bounds.Width * width, bounds.Height);
            }
            DrawCard(canvas, theme, bounds, reveal && progress < 0.5f ? null : moving.CardId, moving.Player, false);
        }
        if (controller.CaptureAnnouncement is { } announcement)
        {
            canvas.OutlinedText(announcement, 38, new UiRect(374, 277, 260, 60), new UiColor(255, 241, 193), new UiColor(35, 20, 15), 2);
        }
        if (!controller.IsAnimating && match.Outcome != TripleTriadOutcome.InProgress)
        {
            string result = match.Outcome == TripleTriadOutcome.PlayerWin ? "YOU WIN" : match.Outcome == TripleTriadOutcome.OpponentWin ? "YOU LOSE" : "DRAW";
            canvas.OutlinedText(result, 42, new UiRect(332, 266, 330, 72), new UiColor(255, 241, 193), new UiColor(35, 20, 15), 2);
        }
        canvas.Text($"{match.Rules}  ·  Round {match.Round + 1}", 16, new UiRect(272, 528, 560, 28), theme.Muted);
    }

    private static void DrawCard(IUiCanvas canvas, UiTheme theme, UiRect bounds, int? id, int owner, bool selected)
    {
        canvas.Fill(new UiRect(bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height), new UiColor(0, 0, 0, 140));
        canvas.ImageStretched(new UiImageSource(id is null ? UiImageKind.CardBack : UiImageKind.CardFace, id ?? 0, owner), bounds);
        if (selected)
        {
            theme.Pointer(canvas, bounds.X - 28, bounds.Y + bounds.Height / 2 - 10, 20);
        }
    }

    private static UiRect CellBounds(int cell) => new(276 + cell % 3 * 136, 94 + cell / 3 * 136, 134, 134);
    private static UiRect HandBounds(int hand, int owner) => new(owner == 0 ? 72 : 768, 124 + hand * 77, 120, 120);

    public override bool Key(UiKey key)
    {
        controller.Navigate(key);
        return true;
    }

    public override void Pointer(float x, float y)
    {
        if (controller.IsAnimating)
        {
            return;
        }
        if (controller.AwaitingRules)
        {
            if (new UiRect(210, 438, 540, 30).Contains(x, y))
            {
                controller.Navigate(UiKey.Confirm);
            }
            else if (new UiRect(210, 472, 540, 30).Contains(x, y))
            {
                controller.Navigate(UiKey.Cancel);
            }
            return;
        }
        if (controller.TradingHand is not null)
        {
            for (int slot = 0; slot < 5; slot++)
            {
                if (new UiRect(40 + slot * 182, 180, 154, 250).Contains(x, y))
                {
                    controller.SelectTradeCard(slot);
                    return;
                }
            }
            return;
        }
        if (controller.Match is null)
        {
            if (new UiRect(24, 94, 398, 444).Contains(x, y))
            {
                int index = scroll + (int)((y - 94) / 37);
                if (index < controller.Available.Count)
                {
                    controller.ChooseCard(controller.Available[index]);
                }
            }
            return;
        }
        for (int hand = 4; hand >= 0; hand--)
        {
            if (HandBounds(hand, 0).Contains(x, y))
            {
                controller.SelectHand(hand);
                return;
            }
        }
        for (int cell = 0; cell < 9; cell++)
        {
            if (CellBounds(cell).Contains(x, y) && controller.ChoosingCell)
            {
                controller.Play(controller.SelectedHand, cell);
                return;
            }
        }
        if (controller.Match.Outcome != TripleTriadOutcome.InProgress)
        {
            controller.Navigate(UiKey.Confirm);
        }
    }
}
