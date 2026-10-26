using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Procedural FF8-style command panels and party ATB gauges.</summary>
internal sealed partial class BattleHudElement : UiElement
{
    private readonly BattleHudController controller;
    private float Scale => Math.Min(Bounds.Width / 960, Bounds.Height / 672);
    private float Left => Bounds.X + (Bounds.Width - 960 * Scale) / 2;
    private bool ShowingMagicList => controller.SelectingAction && !controller.SelectingGuardianFormAction && !controller.HasGuardianFormCommands && !controller.SelectingTarget && controller.SelectedCommand == BattleAbility.Magic;
    private float CommandWidth { get; set; } = 194;
    private float CommandPanelY { get; set; } = 516;
    private float CommandContentY { get; set; } = 530;
    private float CommandPanelHeight { get; set; } = 132;
    private UiRect TargetPanel { get; set; }
    private float TargetContentOffset { get; set; } = 27;
    private float TargetColumnWidth { get; set; }
    private float TargetRowHeight { get; set; }
    private float Top => Bounds.Y + (Bounds.Height - 672 * Scale) / 2;

    public BattleHudElement(BattleHudController controller)
    {
        this.controller = controller;
        Id = "battle-hud";
        Focusable = true;
    }

    private UiRect Rect(float x, float y, float width, float height)
    {
        return new UiRect(Left + x * Scale, Top + y * Scale, width * Scale, height * Scale);
    }

    private void Text(IUiCanvas canvas, UiTheme theme, string value, float x, float y, float width, float height, float size = 18, bool muted = false, UiColor? color = null)
    {
        canvas.Text(value, size * Scale * controller.TextScale, Rect(x, y, width, height), color ?? (muted ? theme.Muted : theme.Foreground));
    }

    private void ChoiceText(IUiCanvas canvas, UiTheme theme, string value, float x, float y, float width, float size, bool muted)
    {
        canvas.PushClip(Rect(x, y, width, 26));
        try
        {
            Text(canvas, theme, value, x, y, Math.Max(width, CommandWidth), 26, size, muted);
        }
        finally
        {
            canvas.PopClip();
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (controller.CardSlotsOwnsInput)
        {
            if (controller.SlotsCards is { } cards && controller.Session.PendingSlotsDeal is { } deal)
            {
                SlotsCardView.Draw(canvas, theme, Bounds, cards, index => controller.Session.GetNativeSlotsCardName(deal.Candidates[index].MagicId), index => deal.Candidates[index].Repetitions, controller.TextScale, controller.SlotsNavigationHint, controller.SlotsConfirmHint);
            }
            return;
        }
        BattleSession session = controller.Session;
        if (session.Outcome is BattleOutcome.Victory or BattleOutcome.Escaped)
        {
            DrawResults(canvas, theme);
            if (controller.ResultOpacity < 1)
            {
                canvas.Fill(Bounds, new UiColor(0, 0, 0, (byte)(255 * (1 - controller.ResultOpacity))));
            }
            return;
        }
        if (controller.NativeDuelState is { } duel)
        {
            DrawNativeDuel(canvas, theme, duel);
            return;
        }
        if (!controller.CommandMenuHidden && controller.BattleMessageDocument is { } battleMessage && session.Outcome == BattleOutcome.Active)
        {
            theme.Panel(canvas, Rect(240, 12, 480, 84));
            if (canvas is IBattleMessageCanvas rich)
            {
                rich.BattleMessage(battleMessage, 20 * Scale * controller.TextScale, Rect(256, 24, 448, 60), controller.BattlePresentationSeconds);
            }
            else
            {
                Text(canvas, theme, battleMessage.Text, 256, 24, 448, 60, 20);
            }
        }
        bool canAttack = !controller.CommandMenuHidden && controller.SelectedActor is not null && session.Outcome == BattleOutcome.Active;
        IReadOnlyList<string> choices = controller.ChoiceLabels;
        string caption = session.Outcome != BattleOutcome.Active ? controller.SystemText("battle.hud.result", "RESULT") : controller.SelectingTarget ? controller.SystemText("battle.hud.target", "TARGET") : controller.SelectingGuardianFormAction ? controller.SystemText("battle.hud.guardian-action", "GF ACTION") : controller.SelectingDrawMode ? controller.SystemText("battle.hud.draw", "DRAW") : controller.SelectingNativeSlots ? controller.SystemText("battle.hud.slots", "SLOTS") : controller.SelectingAction ? controller.CommandLabel(controller.SelectedCommand) : controller.SystemText("battle.hud.command", "COMMAND");
        IReadOnlyList<BattleActionDefinition> actions = controller.SelectingAction && !controller.SelectingNativeSlots && !controller.SelectingDrawMode && !controller.SelectingGuardianFormAction && !controller.HasGuardianFormCommands ? controller.ActionChoices : [];
        string[] values = actions.Select(controller.ChoiceValue).ToArray();
        bool guardianHpColumn = controller.SelectedCommand == BattleAbility.GuardianForce && values.Any(value => value.Length > 0);
        bool[] rightArrows = Enumerable.Range(0, choices.Count).Select(index => actions.Count > index
            ? controller.CanOpenGuardianFormActions(actions[index])
            : !controller.SelectingAction && !controller.HasGuardianFormCommands && controller.NativeLimitAvailable && controller.Commands.ElementAtOrDefault(index) == BattleAbility.Attack).ToArray();
        float arrowWidth = rightArrows.Any(value => value) ? 22 : 0;
        float fontSize = (ShowingMagicList ? 18 : 20) * controller.TextScale;
        float valueWidth = 0;
        float ownerWidth = ShowingMagicList && actions.Any(action => controller.MagicOwnerIcon(action).HasValue) ? 30 : 0;
        if (actions.Count > 0)
        {
            float nameWidth = choices.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            valueWidth = values.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            if (guardianHpColumn)
            {
                float fit = Math.Min(1, Math.Min(24 / fontSize, (440 - 38 - arrowWidth) / Math.Max(1, nameWidth)));
                valueWidth = Math.Max(valueWidth, canvas.MeasureText("9999", fontSize * Scale, 4096 * Scale).Width / Scale) * fit;
                fontSize *= fit;
                CommandWidth = Math.Clamp(38 + arrowWidth + nameWidth * fit, 194, 440) + 12 + valueWidth;
            }
            else if (controller.SelectedCommand == BattleAbility.Draw && valueWidth > 0)
            {
                float fit = Math.Min(1, Math.Min(24 / fontSize, (440 - 38) / Math.Max(1, nameWidth)));
                valueWidth = canvas.MeasureText("100", fontSize * Scale, 4096 * Scale).Width / Scale * fit;
                fontSize *= fit;
                CommandWidth = Math.Clamp(38 + nameWidth * fit, 194, 440) + 12 + valueWidth;
            }
            else
            {
                float fixedWidth = 38 + ownerWidth + arrowWidth + (valueWidth > 0 ? 12 : 0);
                float textWidth = nameWidth + valueWidth;
                float fit = Math.Min(1, Math.Min(24 / fontSize, (440 - fixedWidth) / Math.Max(1, textWidth)));
                fontSize *= fit;
                valueWidth *= fit;
                CommandWidth = Math.Clamp(fixedWidth + textWidth * fit, 194, 440);
            }
        }
        else
        {
            CommandWidth = 194;
        }
        if (controller.NativeSlotsLabel is { } slotsLabel)
        {
            CommandWidth = Math.Clamp(38 + canvas.MeasureText(slotsLabel, fontSize * Scale, 4096 * Scale).Width / Scale, 194, 440);
        }
        int rows = controller.SelectingTarget ? controller.VisibleTargets.Count : choices.Count;
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        float captionContentHeight = Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / Scale;
        float commandOffset = Math.Max(14, captionContentHeight) - 14;
        CommandPanelHeight = (session.Outcome != BattleOutcome.Active ? 76 : 20 + Math.Max(1, Math.Min(4, rows)) * 28 + (controller.SelectingNativeSlots ? 28 : 0)) + commandOffset;
        CommandPanelY = Math.Min(516, 660 - CommandPanelHeight);
        CommandContentY = CommandPanelY + 14 + commandOffset;
        if (controller.SelectingNativeSlots)
        {
            CommandContentY += 28;
        }
        if ((canAttack && !controller.SelectingTarget) || session.Outcome != BattleOutcome.Active)
        {
            theme.Panel(canvas, Rect(30, CommandPanelY, CommandWidth, CommandPanelHeight), caption, captionSize);
        }
        if (session.Outcome != BattleOutcome.Active)
        {
            theme.Pointer(canvas, Left + 38 * Scale, Top + (CommandContentY + 10) * Scale, 12 * Scale);
            Text(canvas, theme, session.CanReturnToField ? controller.SystemText("battle.hud.continue", "Continue") : controller.SystemText("battle.hud.load-retry", "Load / Retry"), 60, CommandContentY + 2, 156, 30, 21);
        }
        else if (!controller.CommandMenuHidden && controller.SelectingTarget)
        {
            DrawTargets(canvas, theme);
        }
        else if (canAttack)
        {
            if (controller.NativeSlotsLabel is { } rollLabel)
            {
                ChoiceText(canvas, theme, rollLabel, 60, CommandContentY - 28, CommandWidth - 38, 20, false);
            }
            int start = Math.Clamp(controller.ChoiceIndex - 1, 0, Math.Max(0, choices.Count - 4));
            for (int row = 0; row < 4 && start + row < choices.Count; row++)
            {
                float y = CommandContentY + row * 28;
                if (start + row == controller.ChoiceIndex)
                {
                    theme.Pointer(canvas, Left + 38 * Scale, Top + (y + 5) * Scale, 10 * Scale);
                }
                bool unavailable = actions.Count > start + row && (!actions[start + row].Available || controller.SelectedCommand == BattleAbility.Magic && controller.SelectedActor is { } caster && !session.QuoteMagic(caster.Id, actions[start + row].Id).Allowed);
                unavailable |= !controller.SelectingAction && !controller.HasGuardianFormCommands && controller.Commands.ElementAtOrDefault(start + row) == BattleAbility.Draw && !controller.DrawAvailable;
                float valueLeft = 30 + CommandWidth - 8 - valueWidth - (guardianHpColumn ? 0 : arrowWidth);
                float ownerLeft = valueLeft - (valueWidth > 0 ? 12 : 0) - ownerWidth - (guardianHpColumn ? arrowWidth : 0);
                UiImageSource? ownerIcon = ShowingMagicList && actions.Count > start + row ? controller.MagicOwnerIcon(actions[start + row]) : null;
                if (ownerIcon is { } portrait)
                {
                    canvas.Image(portrait, Rect(ownerLeft, y, 26, 26));
                }
                ChoiceText(canvas, theme, choices[start + row], 60, y, Math.Max(0, ownerLeft - 60), fontSize / controller.TextScale, unavailable);
                if (rightArrows[start + row])
                {
                    // Native row flag4 uses icon6 and alternates CLUT offsets0/5 every two UI frames.
                    int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate);
                    int palette = (phase & 2) != 0 ? 0 : 5;
                    canvas.Image(new UiImageSource(UiImageKind.Icon, 6, palette), Rect(30 + CommandWidth - 24 - (guardianHpColumn ? 12 + valueWidth : 0), y + 1, 12, 24));
                }
                if (values.Length > start + row)
                {
                    float width = canvas.MeasureText(values[start + row], fontSize * Scale, 4096 * Scale).Width / Scale;
                    ChoiceText(canvas, theme, values[start + row], valueLeft + Math.Max(0, valueWidth - width), y, Math.Min(valueWidth, width), fontSize / controller.TextScale, unavailable);
                }
            }
        }

        IReadOnlyList<BattleCombatant> party = controller.CommandMenuHidden || session.NativeGraphics?.UiServices is { } nativeUi && !(nativeUi.Windows ?? throw new NotSupportedException("Native battle HUD requires its retained UI window bank; migrate the named snapshot explicitly.")).DrawsVitals4B1740(nativeUi.Flags942) ? [] : controller.Party;
        for (int index = 0; index < party.Count; index++)
        {
            BattleCombatant actor = party[index];
            if (session.NativeGraphics?.UiServices is { } rowUi && !(rowUi.Windows ?? throw new NotSupportedException("Native battle HUD requires its retained UI window bank.")).IsVitalsRowVisible4B1740(session.NativeGraphics.ActorIds.IndexOf(actor.Id)))
            {
                continue;
            }
            float y = 528 + index * 38;
            BattleGuardianForceCast? cast = session.GuardianForceCasts.FirstOrDefault(cast => cast.ActorId == actor.Id);
            BattleGuardianForceVital? gf = cast is null ? null : session.GuardianForces.Single(gf => gf.Id == cast.Command.ActionId);
            BattleGuardianFormState? form = session.GetGuardianForm(actor.Id);
            string label = gf is not null ? controller.GuardianForceDisplayName(gf.Id) : form is not null ? controller.GuardianForceDisplayName(form.GuardianForceId) : actor.Name;
            BattlePresentedVital presented = session.ReadDisplayedVital(actor.Id, gf?.Id);
            int hp = presented.HitPoints;
            int maximumHp = presented.MaximumHitPoints;
            bool shownAlive = hp > 0;
            float gauge = cast is null ? actor.Atb / (float)BattleSession.AtbMaximum : cast.InitialTicks == 0 ? 1 : 1 - cast.RemainingTicks / (float)cast.InitialTicks;
            BattleVitalAppearance appearance = session.ReadVitalAppearance(actor.Id, gf?.Id);
            UiColor nameColor = appearance.NameKnockedOut ? new(148, 148, 164) : new(238, 238, 238);
            UiColor hpColor = appearance.HitPointsKnockedOut ? new(255, 41, 24) : appearance.Critical ? new(255, 255, 123) : new(255, 255, 255);
            if (canAttack && controller.SelectedActor?.Id == actor.Id)
            {
                int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate % 16);
                int shade = ((phase < 8 ? phase : 15 - phase) + 2) << 4;
                // Native name/HP glyphs share a 16-frame triangle; 128 is neutral texture modulation.
                nameColor = Modulate(nameColor, shade);
                hpColor = Modulate(hpColor, shade);
            }
            Text(canvas, theme, label, 520, y, 150, 30, 22, color: nameColor);
            float hpWidth = canvas.MeasureText($"{hp,4}", 22 * Scale * controller.TextScale, 4096 * Scale).Width / Scale;
            Text(canvas, theme, $"{hp,4}", 674, y, hpWidth, 30, 22, color: hpColor);
            Text(canvas, theme, $" / {maximumHp,4}", 674 + hpWidth, y, Math.Max(0, 150 - hpWidth), 30, 22, color: new UiColor(255, 255, 255));
            BattleStatusPresentation statuses = session.ReadStatusPresentation(actor.Id);
            DrawGauge(canvas, Rect(830, y + 11, 104, 12), gauge, cast is null && actor.IsReady, shownAlive, cast is not null, statuses.Battle);
            DrawStatuses(canvas, theme, statuses, 520, y + 25, 414);
            if (form is { Phase: BattleGuardianFormPhase.Active })
            {
                Text(canvas, theme, controller.FormatSystemText("battle.hud.remaining-seconds", "{0}s", controller.GuardianFormSeconds(form.RemainingTicks)), 906, y - 5, 42, 18, 12);
            }
        }
        if (session.ActiveAction is { Phase: BattleActionPhase.Recovery } action && session.Events.Any(item => item.Kind == "GunbladeTrigger" && item.ActorId == action.ActorId && session.Frame - item.Frame < BattleSession.AttackRecoveryFrames))
        {
            Text(canvas, theme, controller.SystemText("battle.hud.trigger", "TRIGGER!"), 372, 100, 280, 48, 30);
        }
    }

    private static UiColor Modulate(UiColor color, int shade)
    {
        return new((byte)Math.Min(255, color.R * shade / 128), (byte)Math.Min(255, color.G * shade / 128), (byte)Math.Min(255, color.B * shade / 128), color.A);
    }

    private void DrawTargets(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleCombatant> targets = controller.VisibleTargets;
        float nameWidth = targets.Select(actor => canvas.MeasureText(controller.CombatantDisplayName(actor), 24 * Scale * controller.TextScale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
        float x = 54;
        int columns = Math.Max(1, (targets.Count + 3) / 4);
        TargetColumnWidth = Math.Min(Math.Max(324, 6 * MathF.Floor((nameWidth + 54) / 6)), (496 - x) / columns);
        float textInset = columns > 1 ? 54 : 30;
        float textWidth = TargetColumnWidth - textInset - 12;
        TargetRowHeight = Math.Max(39, canvas.MeasureText("Mg", 24 * Scale * controller.TextScale, float.PositiveInfinity).Height / Scale + 6);
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        TargetContentOffset = Math.Max(27, Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / Scale);
        float panelHeight = 4 * TargetRowHeight + TargetContentOffset + 12;
        float y = Math.Min(609 - 4 * TargetRowHeight, 660 - panelHeight);
        TargetPanel = new UiRect(x, y, TargetColumnWidth * columns, panelHeight);
        string caption = controller.CanChangeTargetSide ? controller.SelectedTarget?.IsPlayer == true ? controller.SystemText("battle.hud.target-allies", "TARGET  < ALLIES >") : controller.SystemText("battle.hud.target-enemies", "TARGET  < ENEMIES >") : controller.SystemText("battle.hud.target", "TARGET");
        theme.Panel(canvas, Rect(TargetPanel.X, TargetPanel.Y, TargetPanel.Width, TargetPanel.Height), caption, captionSize);
        for (int index = 0; index < targets.Count; index++)
        {
            BattleCombatant actor = targets[index];
            float rowY = y + TargetContentOffset + index % 4 * TargetRowHeight;
            float columnX = x + index / 4 * TargetColumnWidth;
            if (controller.SelectingTargetGroup || controller.SelectedTarget?.Id == actor.Id)
            {
                theme.Pointer(canvas, Left + (columnX + (columns > 1 ? 3 : -48)) * Scale, Top + (rowY + 5) * Scale, 24 * Scale);
            }
            canvas.PushClip(Rect(columnX + textInset, rowY, textWidth, TargetRowHeight));
            try
            {
                canvas.Text(controller.CombatantDisplayName(actor), 24 * Scale * controller.TextScale, Rect(columnX + textInset, rowY, textWidth, TargetRowHeight), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
            }
            finally
            {
                canvas.PopClip();
            }
            DrawStatuses(canvas, theme, controller.Session.ReadStatusPresentation(actor.Id), columnX + textInset, rowY + 25, textWidth);
        }
        string help = controller.SelectingTargetGroup ? controller.TargetGroupDescription : controller.PhysicalOverrideCaption;
        float actionHeight = canvas.MeasureText("Mg", 20 * Scale * controller.TextScale, float.PositiveInfinity).Height / Scale;
        float helpHeight = string.IsNullOrEmpty(help) ? 0 : canvas.MeasureText("Mg", 15 * Scale * controller.TextScale, float.PositiveInfinity).Height / Scale + 4;
        float helpPanelHeight = 14 + actionHeight + helpHeight;
        float helpY = y - Math.Max(18, UiTheme.DefaultCaptionFontSize * controller.TextScale * 0.75f + 8) - helpPanelHeight;
        theme.Panel(canvas, Rect(54, helpY, 440, helpPanelHeight));
        Text(canvas, theme, controller.TargetActionCaption, 68, helpY + 7, 412, actionHeight, 20);
        Text(canvas, theme, help, 68, helpY + 7 + actionHeight + 4, 412, helpHeight, 15, true);
    }

    private static void DrawGauge(IUiCanvas canvas, UiRect bounds, float ratio, bool ready, bool alive, bool summoning, uint statuses)
    {
        canvas.Fill(bounds, new UiColor(22, 22, 27));
        UiRect inner = bounds.Inset(2);
        UiRect fill = new(inner.X, inner.Y, inner.Width * Math.Clamp(ratio, 0, 1), inner.Height);
        if (!alive || summoning)
        {
            canvas.Fill(fill, !alive ? new UiColor(75, 75, 80) : new UiColor(64, 128, 255));
            return;
        }
        (UiColor left, UiColor right) = AtbStatusColors(statuses, ready);
        const int bands = 16;
        for (int index = 0; index < bands; index++)
        {
            float amount = index / (float)(bands - 1);
            UiColor color = new((byte)(left.R + (right.R - left.R) * amount), (byte)(left.G + (right.G - left.G) * amount), (byte)(left.B + (right.B - left.B) * amount));
            float x = fill.X + fill.Width * index / bands;
            float next = fill.X + fill.Width * (index + 1) / bands;
            canvas.Fill(new UiRect(x, fill.Y, next - x, fill.Height), color);
        }
    }

    public override bool Key(UiKey key)
    {
        MenuInput? input = key switch
        {
            UiKey.Up => MenuInput.Up,
            UiKey.Down => MenuInput.Down,
            UiKey.Left => MenuInput.Left,
            UiKey.Right => MenuInput.Right,
            UiKey.Confirm => MenuInput.Confirm,
            _ => null
        };
        if (input is null)
        {
            return false;
        }
        controller.Navigate(input.Value);
        return true;
    }

    private int TargetIndex(float x, float y)
    {
        int row = (int)Math.Floor((y - Rect(0, TargetPanel.Y + TargetContentOffset, 0, 0).Y) / (TargetRowHeight * Scale));
        int column = (int)Math.Floor((x - Rect(TargetPanel.X, 0, 0, 0).X) / (TargetColumnWidth * Scale));
        return row is >= 0 and < 4 && column >= 0 ? column * 4 + row : -1;
    }

    public override void PointerHover(float x, float y)
    {
        if (controller.CardSlotsOwnsInput)
        {
            controller.SlotsPointer(x, y, Bounds, false);
            return;
        }
        if (controller.CommandMenuHidden || !controller.SelectingTarget || !Rect(TargetPanel.X, TargetPanel.Y + TargetContentOffset, TargetPanel.Width, TargetPanel.Height - TargetContentOffset).Contains(x, y))
        {
            return;
        }
        int index = TargetIndex(x, y);
        if (index >= 0 && index < controller.VisibleTargets.Count)
        {
            controller.SelectTarget(controller.VisibleTargets[index].Id, false);
        }
    }

    public override void Pointer(float x, float y)
    {
        if (controller.CardSlotsOwnsInput)
        {
            controller.SlotsPointer(x, y, Bounds, true);
            return;
        }
        if (controller.CommandMenuHidden)
        {
            return;
        }
        if (controller.Session.Outcome is BattleOutcome.Victory or BattleOutcome.Escaped)
        {
            if (ResultRect(16, 184, 352, 26).Contains(x, y))
            {
                controller.ConfirmResult();
            }
            return;
        }
        if (controller.CanChangeTargetSide && Rect(TargetPanel.X, TargetPanel.Y - 12, TargetPanel.Width, TargetContentOffset + 12).Contains(x, y))
        {
            controller.ChangeTargetSide();
            return;
        }
        if (controller.SelectingTarget && Rect(TargetPanel.X, TargetPanel.Y, TargetPanel.Width, TargetPanel.Height).Contains(x, y) || !controller.SelectingTarget && Rect(30, CommandPanelY, CommandWidth, Math.Max(132, CommandPanelHeight)).Contains(x, y))
        {
            if (controller.Session.Outcome != BattleOutcome.Active)
            {
                controller.Complete();
            }
            else if (controller.SelectingTarget)
            {
                int index = TargetIndex(x, y);
                if (index >= 0 && index < controller.VisibleTargets.Count)
                {
                    controller.SelectTarget(controller.VisibleTargets[index].Id, true);
                }
            }
            else
            {
                int index = (int)Math.Floor((y - Rect(0, CommandContentY, 0, 0).Y) / (28 * Scale)) + controller.ChoiceStart;
                if (index >= 0 && index < controller.ChoiceLabels.Count)
                {
                    if (x >= Rect(194, 0, 0, 0).X && controller.SelectingAction && !controller.SelectingGuardianFormAction && controller.ActionChoices.ElementAtOrDefault(index) is { } action && controller.CanOpenGuardianFormActions(action))
                    {
                        controller.OpenGuardianFormActions(index);
                    }
                    else
                    {
                        controller.Choose(index);
                    }
                }
            }
        }
        else if (Rect(496, 528, 438, 114).Contains(x, y))
        {
            int index = (int)((y - Rect(0, 528, 0, 0).Y) / (38 * Scale));
            if (index >= 0 && index < controller.Party.Count)
            {
                controller.SelectParty(controller.Party[index].Id);
            }
        }
    }
}
