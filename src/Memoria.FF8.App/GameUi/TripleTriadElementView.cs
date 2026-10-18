using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class TripleTriadElementView(TripleTriadController controller) : UiElement
{
    private UiRect playBounds;
    private UiRect quitBounds;
    private static readonly UiRect SelectionPanel = new(145, 92, 405, 484);
    private static readonly UiRect PreviousPageBounds = new(151, 534, 52, 36);
    private static readonly UiRect NextPageBounds = new(490, 534, 52, 36);
    private static readonly UiColor Blue = new(40, 100, 180);
    private static readonly UiColor Red = new(174, 56, 52);

    public override void Arrange(UiRect bounds)
    {
        Focusable = true;
        base.Arrange(bounds);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (controller.AwaitingRules)
        {
            canvas.ImageStretched(new UiImageSource(UiImageKind.TripleTriadBackground, 0), Bounds);
            DrawRules(canvas, theme);
            DrawPromptFade(canvas);
            return;
        }
        canvas.ImageStretched(new UiImageSource(UiImageKind.TripleTriadBoardBackground, 0), Bounds);
        if (controller.TradingHand is not null)
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
        if (controller.Match is not null || controller.TradingHand is not null)
        {
            canvas.OutlinedText(controller.IsAnimating ? controller.CaptureAnnouncement ?? "" : controller.Message, 20, new UiRect(240, 635, 480, 30), theme.Foreground, new UiColor(0, 0, 0), 1);
        }
        DrawPromptFade(canvas);
    }

    private void DrawRules(IUiCanvas canvas, UiTheme theme)
    {
        const float fontSize = 26;
        const float padding = 18;
        const float choiceIndent = 78;
        string[] rules = controller.Rules == TripleTriadRules.None ? ["Basic rules"] : Enum.GetValues<TripleTriadRules>().Where(rule => rule != TripleTriadRules.None && controller.Rules.HasFlag(rule)).Select(rule => rule switch
        {
            TripleTriadRules.SameWall => "Same Wall",
            TripleTriadRules.SuddenDeath => "Sudden Death",
            _ => rule.ToString()
        }).ToArray();
        string[] lines = ["Rules:", .. rules.Select(rule => "·  " + rule), "·  Trade Rule: " + controller.TradeRuleDescription, "Play", "Quit"];
        UiSize[] sizes = lines.Select(line => canvas.MeasureText(line, fontSize, float.PositiveInfinity)).ToArray();
        float rowHeight = Math.Max(38, sizes.Max(size => size.Height) + 8);
        float width = sizes.Select((size, index) => size.Width + (index >= lines.Length - 2 ? choiceIndent : padding)).Max() + padding + 4;
        float height = 22 + lines.Length * rowHeight + padding;
        UiRect panel = new(Bounds.X + (Bounds.Width - width) / 2, Bounds.Y + (Bounds.Height - height) / 2, width, height);
        theme.Panel(canvas, panel);
        canvas.OutlinedText("INFO.", 14, new UiRect(panel.X + 10, panel.Y + 2, width - 20, 17), theme.Foreground, new UiColor(0, 0, 0), 1);
        for (int index = 0; index < lines.Length; index++)
        {
            float indent = index >= lines.Length - 2 ? choiceIndent : padding;
            UiRect line = new(panel.X + indent, panel.Y + 22 + index * rowHeight, width - indent - padding, rowHeight);
            canvas.Text(lines[index], fontSize, line, theme.Foreground, UiAlignment.Center);
            if (index == lines.Length - 2)
            {
                playBounds = line;
            }
            else if (index == lines.Length - 1)
            {
                quitBounds = line;
            }
        }
        UiRect selected = controller.RulesSelection == 0 ? playBounds : quitBounds;
        theme.Pointer(canvas, selected.X - 62, selected.Y + (rowHeight - 30) / 2, 30);
    }

    private void DrawPromptFade(IUiCanvas canvas)
    {
        if (controller.PromptFade > 0)
        {
            canvas.Fill(Bounds, new UiColor(0, 0, 0, (byte)(Math.Clamp(controller.PromptFade, 0, 1) * 255)));
        }
    }

    private void DrawTrade(IUiCanvas canvas, UiTheme theme)
    {
        canvas.OutlinedText($"Choose your winnings   {controller.TradeSelection.Count} / {controller.RequiredTradeCards}", 25, new UiRect(152, 164, 656, 42), theme.Foreground, new UiColor(0, 0, 0), 1);
        for (int slot = 0; slot < 5; slot++)
        {
            int id = controller.TradingHand![slot];
            UiRect bounds = TradeBounds(slot);
            DrawCard(canvas, theme, bounds, id, controller.TradeSelection.Contains(slot) ? 0 : 1, controller.Selection == slot);
            canvas.OutlinedText(controller.Name(id), 18, new UiRect(bounds.X, bounds.Bottom + 10, bounds.Width, 66), theme.Foreground, new UiColor(0, 0, 0), 1);
            if (controller.TradeSelection.Contains(slot))
            {
                canvas.OutlinedText("Selected", 17, new UiRect(bounds.X, 478, bounds.Width, 28), theme.Foreground, new UiColor(0, 0, 0), 1);
            }
        }
    }

    private void DrawSelection(IUiCanvas canvas, UiTheme theme)
    {
        for (int slot = 0; slot < 5; slot++)
        {
            DrawCard(canvas, theme, HandBounds(slot, 1), null, 1, false);
            if (slot < controller.Deck.Count)
            {
                DrawCard(canvas, theme, HandBounds(slot, 0), controller.Deck[slot], 0, false);
            }
        }
        theme.Panel(canvas, SelectionPanel);
        canvas.OutlinedText("CARDS", 17, new UiRect(155, 91, 270, 25), theme.Foreground, new UiColor(0, 0, 0), 1);
        canvas.OutlinedText("NUM.", 17, new UiRect(465, 91, 76, 25), theme.Foreground, new UiColor(0, 0, 0), 1);
        int first = controller.Selection / TripleTriadController.CardsPerPage * TripleTriadController.CardsPerPage;
        for (int row = 0; row < TripleTriadController.CardsPerPage && row + first < controller.Available.Count; row++)
        {
            int index = row + first;
            int id = controller.Available[index];
            UiRect bounds = SelectionRowBounds(row);
            if (index == controller.Selection)
            {
                theme.Pointer(canvas, 98, bounds.Y + 7, 32);
            }
            UiColor color = controller.Remaining(id) > 0 ? theme.Foreground : theme.Muted;
            canvas.Image(new UiImageSource(UiImageKind.Icon, 215), new UiRect(162, bounds.Y + 5, 28, 31));
            canvas.Text(controller.Name(id), 27, new UiRect(196, bounds.Y, 280, bounds.Height), color, UiAlignment.Center);
            canvas.Text(controller.Remaining(id).ToString(), 27, new UiRect(490, bounds.Y, 50, bounds.Height), color, UiAlignment.Center);
        }
        if (controller.Available.Count > TripleTriadController.CardsPerPage)
        {
            canvas.Text("◀", 23, PreviousPageBounds, theme.Foreground);
            canvas.Text("▶", 23, NextPageBounds, theme.Foreground);
        }
        int selected = controller.Available[controller.Selection];
        DrawCard(canvas, theme, new UiRect(570, 312, 156, 198), selected, 0, false);
    }

    private void DrawMatch(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match)
    {
        IReadOnlyList<IReadOnlyList<int?>> hands = match.Hands;
        IReadOnlyList<TripleTriadPlacedCard?> cards = match.Board;
        IReadOnlyList<TripleTriadElement> elements = match.BoardElements;
        for (int owner = 0; owner < 2; owner++)
        {
            for (int hand = 0; hand < 5; hand++)
            {
                if (hands[owner][hand] is int id)
                {
                    bool selected = owner == 0 && !controller.ChoosingCell && controller.SelectedHand == hand && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating;
                    DrawCard(canvas, theme, HandBounds(hand, owner), owner == 0 || match.Rules.HasFlag(TripleTriadRules.Open) ? id : null, owner, selected);
                }
            }
            canvas.OutlinedText(controller.PresentedScore(owner).ToString(), 26, new UiRect(owner == 0 ? 818 : 108, 648, 48, 24), owner == 0 ? Blue : Red, new UiColor(0, 0, 0), 1.5f);
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
                    canvas.Image(new UiImageSource(UiImageKind.CardElement, (int)elements[cell]), new UiRect(bounds.X + (bounds.Width - 38) / 2, bounds.Y + (bounds.Height - 38) / 2, 38, 38));
                }
                if (selected)
                {
                    canvas.Fill(bounds, new UiColor(255, 239, 179, 42));
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
        if (controller.ChoosingCell && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating)
        {
            UiRect selected = CellBounds(controller.SelectedCell);
            theme.Pointer(canvas, selected.X + 48, selected.Y + 80, 28);
        }
        if (controller.CaptureAnnouncement is { } announcement)
        {
            canvas.OutlinedText(announcement, 38, new UiRect(350, 308, 320, 60), new UiColor(255, 241, 193), new UiColor(35, 20, 15), 2);
        }
        if (!controller.IsAnimating && match.Outcome != TripleTriadOutcome.InProgress)
        {
            string result = match.Outcome == TripleTriadOutcome.PlayerWin ? "YOU WIN" : match.Outcome == TripleTriadOutcome.OpponentWin ? "YOU LOSE" : "DRAW";
            canvas.OutlinedText(result, 42, new UiRect(332, 298, 330, 72), new UiColor(255, 241, 193), new UiColor(35, 20, 15), 2);
        }
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

    private static UiRect CellBounds(int cell) => new(240 + cell % 3 * 160, 48 + cell / 3 * 192, 160, 192);
    private static UiRect HandBounds(int hand, int owner) => new(owner == 0 ? 755 : 45, 24 + hand * 108, 160, 192);
    private static UiRect TradeBounds(int slot) => new(152 + slot * 132, 226, 124, 157);
    private static UiRect SelectionRowBounds(int row) => new(157, 115 + row * 39, 384, 39);

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
            if (playBounds.Contains(x, y))
            {
                controller.SelectRulesChoice(0);
                controller.Navigate(UiKey.Confirm);
            }
            else if (quitBounds.Contains(x, y))
            {
                controller.SelectRulesChoice(1);
                controller.Navigate(UiKey.Confirm);
            }
            return;
        }
        if (controller.TradingHand is not null)
        {
            for (int slot = 0; slot < 5; slot++)
            {
                UiRect card = TradeBounds(slot);
                if (new UiRect(card.X, card.Y, card.Width, 280).Contains(x, y))
                {
                    controller.SelectTradeCard(slot);
                    return;
                }
            }
            return;
        }
        if (controller.Match is null)
        {
            if (PreviousPageBounds.Contains(x, y))
            {
                controller.Navigate(UiKey.Left);
            }
            else if (NextPageBounds.Contains(x, y))
            {
                controller.Navigate(UiKey.Right);
            }
            else
            {
                int first = controller.Selection / TripleTriadController.CardsPerPage * TripleTriadController.CardsPerPage;
                for (int row = 0; row < TripleTriadController.CardsPerPage && first + row < controller.Available.Count; row++)
                {
                    if (SelectionRowBounds(row).Contains(x, y))
                    {
                        controller.ChooseCard(controller.Available[first + row]);
                        return;
                    }
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
