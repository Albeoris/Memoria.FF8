using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class TripleTriadElementView(TripleTriadController controller) : UiElement
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
        DrawBoard(canvas);
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
        if (controller.Match is not null && controller.TradingHand is null || controller.AwaitingTradeConfirmation)
        {
            canvas.OutlinedText(controller.IsAnimating && controller.TradingHand is null ? controller.CaptureAnnouncement ?? "" : controller.Message, 20, new UiRect(160, 635, 640, 30), theme.Foreground, new UiColor(0, 0, 0), 1);
        }
        if (controller.AwaitingReplay)
        {
            theme.Panel(canvas, new UiRect(330, 254, 300, 156));
            canvas.Text(controller.SystemText("card.ui.drawself.play.again", "Play again?"), 24, new UiRect(354, 270, 260, 32), theme.Foreground);
            canvas.Text(controller.SystemText("card.ui.drawself.yes", "Yes"), 24, new UiRect(400, 312, 190, 36), controller.CanReplay ? theme.Foreground : theme.Muted);
            canvas.Text(controller.SystemText("card.ui.drawself.no", "No"), 24, new UiRect(400, 354, 190, 36), theme.Foreground);
            theme.Pointer(canvas, 350, 316 + controller.ReplaySelection * 42, 26);
        }
        DrawPromptFade(canvas);
    }

    private void DrawRules(IUiCanvas canvas, UiTheme theme)
    {
        const float fontSize = 26;
        const float padding = 18;
        const float choiceIndent = 78;
        string[] rules = controller.Rules == TripleTriadRules.None ? [controller.SystemText("card.ui.drawrules.basic.rules", "Basic rules")] : Enum.GetValues<TripleTriadRules>().Where(rule => rule != TripleTriadRules.None && controller.Rules.HasFlag(rule)).Select(rule => rule switch
        {
            TripleTriadRules.SameWall => controller.SystemText("card.ui.drawrules.same.wall", "Same Wall"),
            TripleTriadRules.SuddenDeath => controller.SystemText("card.ui.drawrules.sudden.death", "Sudden Death"),
            TripleTriadRules.Open => controller.SystemText("card.rule.open", "Open"),
            TripleTriadRules.Same => controller.SystemText("card.rule.same", "Same"),
            TripleTriadRules.Plus => controller.SystemText("card.rule.plus", "Plus"),
            TripleTriadRules.Elemental => controller.SystemText("card.rule.elemental", "Elemental"),
            TripleTriadRules.Random => controller.SystemText("card.rule.random", "Random"),
            _ => rule.ToString()
        }).ToArray();
        string[] lines = [controller.SystemText("card.ui.drawrules.rules", "Rules:"), .. rules.Select(rule => "·  " + rule), controller.SystemText("card.ui.drawrules.trade.rule", "·  Trade Rule: ") + controller.TradeRuleDescription, controller.SystemText("card.ui.drawrules.play", "Play"), controller.SystemText("card.ui.drawrules.quit", "Quit")];
        UiSize[] sizes = lines.Select(line => canvas.MeasureText(line, fontSize, float.PositiveInfinity)).ToArray();
        float rowHeight = Math.Max(38, sizes.Max(size => size.Height) + 8);
        float width = sizes.Select((size, index) => size.Width + (index >= lines.Length - 2 ? choiceIndent : padding)).Max() + padding + 4;
        float height = 22 + lines.Length * rowHeight + padding;
        UiRect panel = new(Bounds.X + (Bounds.Width - width) / 2, Bounds.Y + (Bounds.Height - height) / 2, width, height);
        theme.Panel(canvas, panel);
        canvas.OutlinedText(controller.SystemText("card.ui.drawrules.info", "INFO."), 14, new UiRect(panel.X + 10, panel.Y + 2, width - 20, 17), theme.Foreground, new UiColor(0, 0, 0), 1);
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
        bool selecting = controller.TradePhase == TripleTriadTradePhase.Selecting;
        if (controller.IsChoosingTradeCards)
        {
            string prompt = controller.FormatSystemText("card.ui.drawtrade.select.card.s.you.want", "Select {0} card(s) you want", controller.RequiredTradeCards);
            float textWidth = canvas.MeasureText(prompt, 25, float.PositiveInfinity).Width;
            theme.Panel(canvas, new UiRect(480 - (textWidth + 40) / 2, 24, textWidth + 40, 84));
            canvas.OutlinedText(prompt, 25, new UiRect(480 - textWidth / 2, 24, textWidth, 84), theme.Foreground, new UiColor(0, 0, 0), 1);
        }
        for (int index = 0; index < controller.TradeCards.Count; index++)
        {
            if (controller.IsTradeCardRemoved(index) || index == controller.TradingCardIndex)
            {
                continue;
            }
            DrawExchangeCard(index);
        }
        if (controller.TradingCardIndex >= 0)
        {
            DrawExchangeCard(controller.TradingCardIndex);
        }
        if (controller.TradePhase == TripleTriadTradePhase.ConfirmingSelection)
        {
            theme.Panel(canvas, new UiRect(330, 254, 300, 156));
            canvas.Text(controller.SystemText("card.ui.drawtrade.are.you.sure", "Are you sure?"), 24, new UiRect(354, 270, 260, 32), theme.Foreground);
            canvas.Text(controller.SystemText("card.ui.drawtrade.yes", "Yes"), 24, new UiRect(400, 312, 190, 36), theme.Foreground);
            canvas.Text(controller.SystemText("card.ui.drawtrade.no", "No"), 24, new UiRect(400, 354, 190, 36), theme.Foreground);
            theme.Pointer(canvas, 350, 316 + controller.TradeConfirmationSelection * 42, 26);
        }

        void DrawExchangeCard(int index)
        {
            int id = controller.TradeCards[index];
            UiRect bounds = ExchangeBounds(index);
            float width = controller.PresentedTradeWidth(index);
            UiRect turned = new(bounds.X + bounds.Width * (1 - width) / 2, bounds.Y, bounds.Width * width, bounds.Height);
            bool selected = selecting && index == controller.Selection + controller.TradeCards.Count / 2;
            DrawCard(canvas, theme, turned, controller.PresentedTradeBack(index) ? null : id, controller.PresentedTradeOwner(index), selected);
            if (controller.TradingCardIndex < 0 || index == controller.TradingCardIndex && controller.AwaitingTradeConfirmation)
            {
                UiColor color = TradeNameColor(id, theme);
                string name = controller.TradeCardName(id);
                float fontSize = 18;
                float textWidth = canvas.MeasureText(name, fontSize, float.PositiveInfinity).Width;
                if (textWidth > bounds.Width - 8)
                {
                    fontSize *= (bounds.Width - 8) / textWidth;
                    textWidth = canvas.MeasureText(name, fontSize, float.PositiveInfinity).Width;
                }
                canvas.OutlinedText(name, fontSize, new UiRect(bounds.X + (bounds.Width - textWidth) / 2, bounds.Bottom + 5, textWidth, 28), color, new UiColor(0, 0, 0), 1);
            }
        }
    }

    private UiColor TradeNameColor(int id, UiTheme theme) => !controller.HasAcquiredCard(id) ? new UiColor(100, 149, 237) : controller.CollectionCount(id) == 0 ? new UiColor(255, 255, 0) : theme.Foreground;

    private UiRect ExchangeBounds(int index)
    {
        if (controller.TradeCards.Count != 10)
        {
            int half = controller.TradeCards.Count / 2;
            TripleTriadLayout layout = new(Layout.Width, Layout.Height, half);
            UiRect sourceBounds = layout.Hand(index % half, index < half ? 0 : 1);
            if (index != controller.TradingCardIndex)
            {
                return sourceBounds;
            }
            float move = (float)controller.TradeProgress;
            return new UiRect(sourceBounds.X + (400 - sourceBounds.X) * move, sourceBounds.Y + (240 - sourceBounds.Y) * move, sourceBounds.Width, sourceBounds.Height);
        }
        float x = (index % 5 - 2) * 64;
        float y = index < 5 ? 40 : -40;
        float z = 512;
        float progress = (float)controller.TradeProgress;
        if (controller.TradePhase == TripleTriadTradePhase.Entering)
        {
            float source = index < 5 ? 416 : -416;
            x = source + (x - source) * MathF.Sin(progress * MathF.PI / 2);
        }
        if (index == controller.TradingCardIndex)
        {
            if (controller.TradePhase == TripleTriadTradePhase.Moving)
            {
                x *= 1 - progress;
                y = y * (1 - progress) - 112 * MathF.Sin(progress * MathF.PI);
                z = 512 - 256 * progress;
            }
            else
            {
                x = 0;
                y = controller.TradePhase == TripleTriadTradePhase.Exiting ? (controller.PresentedTradeOwner(index) == 0 ? 112 : -112) * progress : 0;
                z = 256;
            }
        }
        else if (controller.TradePhase == TripleTriadTradePhase.Flipping && controller.PresentedTradeWidth(index) < 1)
        {
            z -= 32 * MathF.Sin(progress * MathF.PI);
        }
        float scale = 512 / z;
        return new UiRect(480 + (x - 32) * 2.5f * scale, 336 + (y - 32) * 3 * scale, 160 * scale, 192 * scale);
    }

    private void DrawSelection(IUiCanvas canvas, UiTheme theme)
    {
        for (int slot = 0; slot < controller.Options.HandSize; slot++)
        {
            DrawCard(canvas, theme, HandBounds(slot, 1), null, 1, false);
            if (slot < controller.Deck.Count)
            {
                DrawCard(canvas, theme, HandBounds(slot, 0), controller.Deck[slot], 0, false);
            }
        }
        theme.Panel(canvas, SelectionPanel);
        canvas.OutlinedText(controller.SystemText("card.ui.drawselection.cards", "CARDS"), 17, new UiRect(155, 91, 270, 25), theme.Foreground, new UiColor(0, 0, 0), 1);
        canvas.OutlinedText(controller.SystemText("card.ui.drawselection.num", "NUM."), 17, new UiRect(465, 91, 76, 25), theme.Foreground, new UiColor(0, 0, 0), 1);
        int historyRows = controller.SelectionPage == 0 ? controller.RecentDecks.Count : 0;
        for (int index = 0; index < historyRows; index++)
        {
            UiRect bounds = RecentDeckRowBounds(index);
            if (controller.RecentDeckSelection == index)
            {
                theme.Pointer(canvas, 98, bounds.Y + 14, 32);
            }
            canvas.Text(controller.FormatSystemText("card.ui.drawselection.recent.deck", "Recent deck {0}", index + 1), 20, new UiRect(162, bounds.Y, 160, bounds.Height), theme.Foreground, UiAlignment.Center);
            for (int slot = 0; slot < controller.Options.HandSize; slot++)
            {
                UiRect cardBounds = new(326 + slot * Math.Min(42, 204f / controller.Options.HandSize), bounds.Y + 4, Math.Min(36, 190f / controller.Options.HandSize), 46);
                DrawCard(canvas, theme, cardBounds, controller.RecentDecks[index][slot], 0, false, !controller.IsRecentDeckCardAvailable(index, slot));
            }
        }
        int first = controller.FirstVisibleCard;
        for (int row = 0; row < controller.VisibleCardCount; row++)
        {
            int index = row + first;
            int id = controller.Available[index];
            UiRect bounds = SelectionRowBounds(row, historyRows);
            if (controller.RecentDeckSelection < 0 && index == controller.Selection)
            {
                theme.Pointer(canvas, 98, bounds.Y + 7, 32);
            }
            UiColor color = controller.Remaining(id) > 0 ? theme.Foreground : theme.Muted;
            canvas.Image(new UiImageSource(UiImageKind.Icon, 215), new UiRect(162, bounds.Y + 5, 28, 31));
            canvas.Text(controller.Name(id), 27, new UiRect(196, bounds.Y, 280, bounds.Height), color, UiAlignment.Center);
            canvas.Text(controller.Remaining(id).ToString(), 27, new UiRect(490, bounds.Y, 50, bounds.Height), color, UiAlignment.Center);
        }
        if (controller.SelectionPageCount > 1)
        {
            canvas.Text("◀", 23, PreviousPageBounds, theme.Foreground);
            canvas.Text("▶", 23, NextPageBounds, theme.Foreground);
        }
        int selected = controller.Available[controller.Selection];
        DrawCard(canvas, theme, new UiRect(570, 312, 156, 198), selected, 0, false);
        canvas.OutlinedText(controller.Message, 20, new UiRect(152, 586, 580, 64), theme.Foreground, new UiColor(0, 0, 0), 1);
    }

    private void DrawMatch(IUiCanvas canvas, UiTheme theme, TripleTriadMatch match)
    {
        IReadOnlyList<IReadOnlyList<int?>> hands = match.Hands;
        IReadOnlyList<TripleTriadPlacedCard?> cards = match.Board;
        IReadOnlyList<TripleTriadElement> elements = match.BoardElements;
        for (int owner = 0; owner < 2; owner++)
        {
            for (int hand = 0; hand < match.HandSize; hand++)
            {
                if (hands[owner][hand] is int id)
                {
                    bool selected = owner == 0 && !controller.ChoosingCell && controller.SelectedHand == hand && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating;
                    DrawCard(canvas, theme, HandBounds(hand, owner), owner == 0 || match.Rules.HasFlag(TripleTriadRules.Open) ? id : null, owner, selected);
                }
            }
            canvas.OutlinedText(controller.PresentedScore(owner).ToString(), 26, new UiRect(owner == 0 ? 818 : 108, 648, 48, 24), owner == 0 ? Blue : Red, new UiColor(0, 0, 0), 1.5f);
        }
        for (int cell = 0; cell < controller.Match!.Board.Count; cell++)
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
                DrawCard(canvas, theme, turned, placed.CardId, owner, false, modifier: match.GetElementModifier(placed.CardId, cell));
                if (match.Rules.HasFlag(TripleTriadRules.Elemental) && elements[cell] != TripleTriadElement.None && width > 0.8 && height > 0.8)
                {
                    string modifier = controller.Card(placed.CardId).Element == elements[cell] ? "+1" : "−1";
                    UiColor modifierColor = match.GetElementModifier(placed.CardId, cell) > 0 ? new UiColor(90, 235, 105) : new UiColor(245, 75, 65);
                    canvas.OutlinedText(modifier, 23, new UiRect(bounds.Right - 36, bounds.Bottom - 31, 35, 27), modifierColor, new UiColor(23, 18, 14), 1.4f);
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
        DrawAssistance(canvas, theme, match);
        DrawRedraw(canvas, theme);
        if (controller.ChoosingCell && match.Outcome == TripleTriadOutcome.InProgress && !controller.IsAnimating)
        {
            UiRect selected = CellBounds(controller.SelectedCell);
            theme.Pointer(canvas, selected.X + selected.Width * 0.3f, selected.Y + selected.Height * 0.42f, Math.Min(28, selected.Width / 4));
        }
        if (controller.CaptureAnnouncement is { } announcement)
        {
            canvas.OutlinedText(announcement, 38, new UiRect(350, 308, 320, 60), new UiColor(255, 241, 193), new UiColor(35, 20, 15), 2);
        }
        if (!controller.IsAnimating && match.Outcome != TripleTriadOutcome.InProgress)
        {
            int outcome = match.Outcome == TripleTriadOutcome.PlayerWin ? 0 : match.Outcome == TripleTriadOutcome.OpponentWin ? 1 : 2;
            canvas.Image(new UiImageSource(UiImageKind.TripleTriadOutcome, outcome), new UiRect(188, 292, 584, 110));
        }
    }

    private static void DrawCard(IUiCanvas canvas, UiTheme theme, UiRect bounds, int? id, int owner, bool selected, bool unavailable = false, int modifier = 0)
    {
        canvas.Fill(new UiRect(bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height), new UiColor(0, 0, 0, 140));
        canvas.ImageStretched(new UiImageSource(id is null ? UiImageKind.CardBack : unavailable ? UiImageKind.CardFaceGrayscale : UiImageKind.CardFace, id ?? 0, owner, modifier), bounds);
        if (selected)
        {
            theme.Pointer(canvas, bounds.X - 28, bounds.Y + bounds.Height / 2 - 10, 20);
        }
    }

    private UiRect CellBounds(int cell) => Layout.Cell(cell);
    private UiRect HandBounds(int hand, int owner) => Layout.Hand(hand, owner);
    private static UiRect RecentDeckRowBounds(int row) => new(157, 115 + row * 56, 384, 56);
    private static UiRect SelectionRowBounds(int row, int historyRows) => new(157, 115 + historyRows * 56 + row * 39, 384, 39);

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
        if (controller.AwaitingReplay)
        {
            for (int choice = 0; choice < 2; choice++)
            {
                if (new UiRect(350, 312 + choice * 42, 250, 36).Contains(x, y))
                {
                    if (choice == 0 && !controller.CanReplay)
                    {
                        return;
                    }
                    controller.SelectReplayChoice(choice);
                    controller.Navigate(UiKey.Confirm);
                    break;
                }
            }
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
            if (controller.TradePhase == TripleTriadTradePhase.ConfirmingSelection)
            {
                for (int choice = 0; choice < 2; choice++)
                {
                    if (new UiRect(350, 312 + choice * 42, 250, 36).Contains(x, y))
                    {
                        if (controller.TradeConfirmationSelection != choice)
                        {
                            controller.Navigate(UiKey.Down);
                        }
                        controller.Navigate(UiKey.Confirm);
                        break;
                    }
                }
                return;
            }
            if (controller.AwaitingTradeConfirmation)
            {
                controller.Navigate(UiKey.Confirm);
                return;
            }
            for (int slot = 0; slot < controller.Options.HandSize; slot++)
            {
                UiRect card = ExchangeBounds(slot + controller.TradeCards.Count / 2);
                if (card.Contains(x, y))
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
                int historyRows = controller.SelectionPage == 0 ? controller.RecentDecks.Count : 0;
                for (int index = 0; index < historyRows; index++)
                {
                    if (RecentDeckRowBounds(index).Contains(x, y))
                    {
                        controller.ChooseDeck(index);
                        return;
                    }
                }
                int first = controller.FirstVisibleCard;
                for (int row = 0; row < controller.VisibleCardCount; row++)
                {
                    if (SelectionRowBounds(row, historyRows).Contains(x, y))
                    {
                        controller.ChooseCard(controller.Available[first + row]);
                        return;
                    }
                }
            }
            return;
        }
        if (controller.AwaitingOpeningConfirmation && StartBounds.Contains(x, y))
        {
            controller.Navigate(UiKey.Confirm);
            return;
        }
        if (controller.CanRedraw && RedrawBounds.Contains(x, y))
        {
            controller.Redraw();
            return;
        }
        for (int hand = controller.Match.HandSize - 1; hand >= 0; hand--)
        {
            if (HandBounds(hand, 0).Contains(x, y))
            {
                controller.SelectHand(hand);
                return;
            }
        }
        for (int cell = 0; cell < controller.Match!.Board.Count; cell++)
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
