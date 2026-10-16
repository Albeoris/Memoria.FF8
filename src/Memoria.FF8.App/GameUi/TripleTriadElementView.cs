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
        canvas.Text(controller.Match is { } match ? $"YOU  {match.PlayerScore}  :  {match.OpponentScore}  OPPONENT" : $"Choose your hand   {controller.Deck.Count} / 5", 20, new UiRect(450, 28, 470, 30), theme.Foreground);
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
        canvas.Text(controller.Message, 20, new UiRect(32, 578, 896, 32), theme.Foreground);
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
        theme.Panel(canvas, new UiRect(16, 80, 390, 474));
        scroll = Math.Clamp(scroll, Math.Max(0, controller.Selection - 11), controller.Selection);
        for (int row = 0; row < 12 && row + scroll < controller.Available.Count; row++)
        {
            int index = row + scroll;
            int id = controller.Available[index];
            UiRect bounds = new(28, 93 + row * 37, 366, 35);
            if (index == controller.Selection)
            {
                canvas.Fill(bounds, theme.Highlight);
            }
            canvas.Text($"{controller.Name(id)}  ×{controller.Remaining(id)}", 18, bounds.Inset(6), controller.Remaining(id) > 0 ? theme.Foreground : theme.Muted);
        }
        int selected = controller.Available[controller.Selection];
        DrawCard(canvas, theme, new UiRect(426, 90, 210, 210), selected, 0, true);
        canvas.Text(controller.Name(selected), 23, new UiRect(426, 314, 480, 38), theme.Foreground);
        canvas.Text($"Lv {selected / 11 + 1} · {controller.Card(selected).Element}\nRules: {controller.Rules}", 18, new UiRect(426, 362, 490, 90), theme.Foreground);
        canvas.Text("Selected: " + string.Join(", ", controller.Deck.Select(controller.Name)), 17, new UiRect(426, 465, 490, 80), theme.Foreground);
    }

    private void DrawMatch(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match)
    {
        canvas.Text("YOUR HAND", 16, new UiRect(20, 80, 180, 27), Blue);
        canvas.Text("OPPONENT", 16, new UiRect(780, 80, 180, 27), Red);
        for (int hand = 0; hand < 5; hand++)
        {
            if (match.Hands[0][hand] is int id)
            {
                DrawCard(canvas, theme, HandBounds(hand, 0), id, 0, !controller.ChoosingCell && controller.SelectedHand == hand && match.Outcome == TripleTriadOutcome.InProgress);
            }
            if (match.Hands[1][hand] is int other)
            {
                DrawCard(canvas, theme, HandBounds(hand, 1), (match.Rules & TripleTriadRules.Open) != 0 ? other : null, 1, false);
            }
        }
        for (int cell = 0; cell < 9; cell++)
        {
            UiRect bounds = CellBounds(cell);
            bool selected = controller.ChoosingCell && controller.SelectedCell == cell && match.Outcome == TripleTriadOutcome.InProgress;
            if (match.Board[cell] is { } placed)
            {
                DrawCard(canvas, theme, bounds, placed.CardId, placed.Owner, selected);
                if (match.Rules.HasFlag(TripleTriadRules.Elemental) && match.BoardElements[cell] != TripleTriadElement.None)
                {
                    string modifier = controller.Card(placed.CardId).Element == match.BoardElements[cell] ? "+1" : "-1";
                    canvas.Fill(new UiRect(bounds.Right - 35, bounds.Bottom - 24, 30, 19), new UiColor(8, 13, 24, 218));
                    canvas.Text(modifier, 15, new UiRect(bounds.Right - 32, bounds.Bottom - 23, 28, 20), theme.Foreground);
                }
            }
            else
            {
                canvas.Fill(bounds, selected ? new UiColor(100, 109, 96) : new UiColor(31, 41, 55));
                canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y, theme.Muted, 1);
                canvas.Line(bounds.X, bounds.Y, bounds.X, bounds.Bottom, theme.Muted, 1);
                string element = match.BoardElements[cell] == TripleTriadElement.None ? (cell + 1).ToString() : match.BoardElements[cell].ToString();
                canvas.Text(element, 18, bounds.Inset(15), theme.Muted, UiAlignment.Center);
            }
        }
        canvas.Text($"{match.Rules}  ·  Round {match.Round + 1}", 15, new UiRect(206, 518, 550, 42), theme.Muted);
    }

    private void DrawCard(IUiCanvas canvas, UiTheme theme, UiRect bounds, int? id, int owner, bool selected)
    {
        UiColor edge = selected ? new UiColor(255, 222, 114) : owner == 0 ? Blue : Red;
        canvas.Fill(bounds, edge);
        canvas.Image(new UiImageSource(id is null ? UiImageKind.CardBack : UiImageKind.Card, id ?? 0), bounds.Inset(selected ? 5 : 3));
        if (id is not int cardId)
        {
            return;
        }
        TripleTriadCard card = controller.Card(cardId);
        float scale = bounds.Width / 126;
        float x = bounds.X + 5;
        float y = bounds.Y + 4;
        canvas.Fill(new UiRect(x, y, 49 * scale, 51 * scale), new UiColor(8, 13, 24, 218));
        Rank(card.Top, x + 18 * scale, y);
        Rank(card.Left, x, y + 17 * scale);
        Rank(card.Right, x + 35 * scale, y + 17 * scale);
        Rank(card.Bottom, x + 18 * scale, y + 34 * scale);
        if (card.Element != TripleTriadElement.None)
        {
            canvas.Fill(new UiRect(bounds.X + 4, bounds.Bottom - 22, bounds.Width - 8, 18), new UiColor(8, 13, 24, 218));
            canvas.Text(card.Element.ToString(), 12, new UiRect(bounds.X + 8, bounds.Bottom - 21, bounds.Width - 12, 20), theme.Foreground);
        }

        void Rank(int value, float rankX, float rankY)
        {
            canvas.Text(value == 10 ? "A" : value.ToString(), 15 * scale, new UiRect(rankX, rankY, 18 * scale, 21 * scale), theme.Foreground);
        }
    }

    private static UiRect CellBounds(int cell) => new(266 + cell % 3 * 140, 88 + cell / 3 * 140, 136, 136);
    private static UiRect HandBounds(int hand, int owner) => new(owner == 0 ? 32 : 802, 112 + hand * 87, 78, 78);

    public override bool Key(UiKey key)
    {
        controller.Navigate(key);
        return true;
    }

    public override void Pointer(float x, float y)
    {
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
            if (new UiRect(28, 93, 366, 444).Contains(x, y))
            {
                int index = scroll + (int)((y - 93) / 37);
                if (index < controller.Available.Count)
                {
                    controller.ChooseCard(controller.Available[index]);
                }
            }
            return;
        }
        for (int hand = 0; hand < 5; hand++)
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
