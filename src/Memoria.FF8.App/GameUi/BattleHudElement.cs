using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Procedural FF8-style command panels and party ATB gauges.</summary>
internal sealed partial class BattleHudElement : UiElement
{
    private readonly BattleHudController controller;
    private BattleHudLayout Layout => new(Bounds);
    private float Scale => Layout.Scale;
    private float Left => Layout.Left;
    private float CommandWidth { get; set; } = 194;
    private float CommandPanelY { get; set; } = 516;
    private float CommandContentY { get; set; } = 530;
    private float CommandPanelHeight { get; set; } = 132;
    private BattleHudTargetList targetList;
    private string[] drawnTargetIds = [];
    private (UiRect Bounds, float TextScale) drawnTargetLayout;
    private int? requestedTargetListStart;
    private string? lastDrawnTargetId;
    private UiRect targetHelpBounds;
    private BattleHudPartyList partyList;
    private string[] drawnPartyIds = [];
    private string? lastDrawnPartySelection;
    private (UiRect Bounds, float TextScale) drawnPartyLayout;
    private int? requestedPartyStart;
    private bool drewExpandedParty;
    private float Top => Layout.Top;

    public BattleHudElement(BattleHudController controller)
    {
        this.controller = controller;
        Id = "battle-hud";
        Focusable = true;
    }

    private UiRect Rect(float x, float y, float width, float height)
    {
        return Layout.Rect(x, y, width, height);
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
        controller.BeginPresentationDraw();
        try
        {
            DrawPresentation(canvas, theme);
        }
        finally
        {
            controller.EndPresentationDraw();
        }
    }

    private void DrawPresentation(IUiCanvas canvas, UiTheme theme)
    {
        drewExpandedParty = false;
        if (controller.CardSlotsOwnsInput)
        {
            if (controller.SlotsCards is { } cards && controller.Session.PendingSlotsDeal is { } deal)
            {
                SlotsCardView.Draw(canvas, theme, Bounds, cards, index => controller.SlotsCardLabel(deal.Candidates[index]), index => deal.Candidates[index].Repetitions, controller.TextScale, controller.SlotsNavigationHint, controller.SlotsConfirmHint);
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
        if (controller.NativeShotState is { } shot)
        {
            DrawNativeShot(canvas, theme, shot);
            return;
        }
        if (controller.NativeDuelState is { } duel)
        {
            DrawNativeDuel(canvas, theme, duel);
            return;
        }
        if (!controller.CommandMenuHidden && controller.BattleMessageDocument is { } battleMessage && session.Outcome == BattleOutcome.Active)
        {
            theme.Panel(canvas, Layout.MessagePanel);
            float messageSize = (controller.BattleMessageIsActionTitle ? 26 : 20) * Scale * controller.TextScale;
            if (canvas is IBattleMessageCanvas rich)
            {
                rich.BattleMessage(battleMessage, messageSize, Layout.MessageText, controller.BattlePresentationSeconds);
            }
            else
            {
                canvas.AlignedText(battleMessage.Text, messageSize, Layout.MessageText, theme.Foreground, UiAlignment.Center);
            }
        }
        BattleCombatant? selectedActor = controller.SelectedActor;
        bool canAttack = !controller.CommandMenuHidden && selectedActor is not null && session.Outcome == BattleOutcome.Active;
        bool targetsVisible = !controller.CommandMenuHidden && session.Outcome == BattleOutcome.Active && controller.SelectingTarget;
        IReadOnlyList<BattleCombatant> party = controller.Party;
        bool compactParty = party.Count >= 4;
        if (party.Count > 4 && !controller.CommandMenuHidden)
        {
            DrawExpandedParty(canvas, theme, party, selectedActor, canAttack);
        }
        IReadOnlyList<string> choices = controller.SelectingTarget ? [] : controller.ChoiceLabels;
        string caption = session.Outcome != BattleOutcome.Active ? controller.SystemText("battle.hud.result", "RESULT") : controller.SelectingTarget ? controller.SystemText("battle.hud.target", "TARGET") : controller.SelectingGuardianFormAction ? controller.SystemText("battle.hud.guardian-action", "GF ACTION") : controller.SelectingNativeSlots ? controller.SystemText("battle.hud.slots", "SLOTS") : controller.SelectingAction ? controller.CommandLabel(controller.SelectedCommand) : controller.SystemText("battle.hud.command", "COMMAND");
        IReadOnlyList<BattleActionDefinition> actions = !controller.SelectingTarget && controller.SelectingAction && !controller.ShowingDrawList && !controller.ShowingMagicList && !controller.SelectingNativeSlots && !controller.SelectingGuardianFormAction && !controller.HasGuardianFormCommands ? controller.ActionChoices : [];
        string[] values = actions.Select(controller.ChoiceValue).ToArray();
        bool guardianHpColumn = controller.SelectedCommand == BattleAbility.GuardianForce && values.Any(value => value.Length > 0);
        bool[] rightArrows = Enumerable.Range(0, choices.Count).Select(index => actions.Count > index
            ? controller.CanOpenGuardianFormActions(actions[index])
            : !controller.SelectingAction && !controller.HasGuardianFormCommands && controller.NativeLimitAvailable && controller.Commands.ElementAtOrDefault(index) == BattleAbility.Attack).ToArray();
        float arrowWidth = rightArrows.Any(value => value) ? 22 : 0;
        float fontSize = 20 * controller.TextScale;
        float valueWidth = 0;
        if (actions.Count > 0)
        {
            float nameWidth = choices.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            valueWidth = values.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            if (guardianHpColumn)
            {
                valueWidth = Math.Max(valueWidth, canvas.MeasureText("9999", fontSize * Scale, 4096 * Scale).Width / Scale);
                float fit = Math.Min(1, Math.Min(24 / fontSize, (440 - 38 - arrowWidth) / Math.Max(1, nameWidth)));
                if (compactParty)
                {
                    float available = BattleHudLayout.PartyLeft - BattleHudLayout.CommandX - 38 - arrowWidth - 12;
                    fit = Math.Min(fit, available / Math.Max(1, nameWidth + valueWidth));
                }
                valueWidth *= fit;
                fontSize *= fit;
                CommandWidth = Math.Clamp(38 + arrowWidth + nameWidth * fit, 194, 440) + 12 + valueWidth;
            }
            else
            {
                float fixedWidth = 38 + arrowWidth + (valueWidth > 0 ? 12 : 0);
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
        if (party.Count > 4 && !controller.SelectingAction)
        {
            CommandWidth = 194;
        }
        if (drewExpandedParty)
        {
            float maximumWidth = partyList.Panel.X - BattleHudLayout.CommandX - 12;
            float fit = Math.Min(1, Math.Max(0, maximumWidth - 38) / Math.Max(1, CommandWidth - 38));
            CommandWidth = Math.Min(CommandWidth, maximumWidth);
            fontSize *= fit;
            valueWidth *= fit;
        }
        int rows = controller.SelectingTarget ? controller.VisibleTargets.Count : choices.Count;
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        float captionContentHeight = Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / Scale;
        float commandOffset = Math.Max(14, captionContentHeight) - 14;
        CommandPanelHeight = session.Outcome != BattleOutcome.Active ? 76 + commandOffset : BattleHudLayout.CommandPanel(CommandWidth, rows, captionContentHeight, controller.SelectingNativeSlots ? 28 : 0).Height;
        CommandPanelY = Math.Min(516, 660 - CommandPanelHeight);
        CommandContentY = CommandPanelY + 14 + commandOffset;
        if (controller.SelectingNativeSlots)
        {
            CommandContentY += 28;
        }
        if ((canAttack && !controller.SelectingTarget && !controller.ShowingDrawList && !controller.ShowingMagicList) || session.Outcome != BattleOutcome.Active)
        {
            theme.Panel(canvas, Rect(30, CommandPanelY, CommandWidth, CommandPanelHeight), caption, captionSize);
        }
        if (session.Outcome != BattleOutcome.Active)
        {
            theme.Pointer(canvas, Left + 38 * Scale, Top + (CommandContentY + 10) * Scale, 12 * Scale);
            Text(canvas, theme, session.CanReturnToField ? controller.SystemText("battle.hud.continue", "Continue") : controller.SystemText("battle.hud.load-retry", "Load / Retry"), 60, CommandContentY + 2, 156, 30, 21);
        }
        else if (targetsVisible)
        {
            DrawNativeParty(canvas, theme, party, compactParty, selectedActor, canAttack);
            DrawTargets(canvas, theme);
        }
        else if (canAttack && !controller.ShowingDrawList && !controller.ShowingMagicList)
        {
            if (controller.NativeSlotsLabel is { } rollLabel)
            {
                ChoiceText(canvas, theme, rollLabel, 60, CommandContentY - 28, CommandWidth - 38, 20, false);
            }
            int start = BattleHudLayout.CommandStart(controller.ChoiceIndex, choices.Count);
            for (int row = 0; row < BattleHudLayout.VisibleCommandRows && start + row < choices.Count; row++)
            {
                float y = CommandContentY + row * BattleHudLayout.CommandRowHeight;
                if (start + row == controller.ChoiceIndex)
                {
                    Layout.DrawCommandPointer(canvas, theme, BattleHudLayout.CommandX, y);
                }
                bool unavailable = actions.Count > start + row && (!actions[start + row].Available || controller.SelectedCommand == BattleAbility.Magic && controller.SelectedActor is { } caster && !session.QuoteMagic(caster.Id, actions[start + row].Id).Allowed);
                unavailable |= !controller.SelectingAction && !controller.HasGuardianFormCommands && controller.Commands.ElementAtOrDefault(start + row) == BattleAbility.Draw && !controller.DrawAvailable;
                unavailable |= !controller.SelectingAction && !controller.HasGuardianFormCommands && controller.Commands.ElementAtOrDefault(start + row) == BattleAbility.Magic && !controller.MagicAvailable;
                float valueLeft = 30 + CommandWidth - 8 - valueWidth - (guardianHpColumn ? 0 : arrowWidth);
                float ownerLeft = valueLeft - (valueWidth > 0 ? 12 : 0) - (guardianHpColumn ? arrowWidth : 0);
                if (!DrawNativeLimitCommandRow(canvas, theme, start + row, y, Math.Max(0, ownerLeft - 60), fontSize / controller.TextScale, unavailable))
                {
                    ChoiceText(canvas, theme, choices[start + row], 60, y, Math.Max(0, ownerLeft - 60), fontSize / controller.TextScale, unavailable);
                }
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

        if (!targetsVisible)
        {
            DrawNativeParty(canvas, theme, party, compactParty, selectedActor, canAttack);
        }
        if (canAttack && controller.ShowingDrawList)
        {
            DrawDrawList(canvas, theme);
        }
        else if (canAttack && controller.ShowingMagicList)
        {
            DrawMagicList(canvas, theme);
        }
        if (session.ActiveAction is { Phase: BattleActionPhase.Recovery } action && session.Events.Any(item => item.Kind == "GunbladeTrigger" && item.ActorId == action.ActorId && session.Frame - item.Frame < BattleSession.AttackRecoveryFrames))
        {
            Text(canvas, theme, controller.SystemText("battle.hud.trigger", "TRIGGER!"), 372, 100, 280, 48, 30);
        }
    }

    private void DrawNativeParty(IUiCanvas canvas, UiTheme theme, IReadOnlyList<BattleCombatant> party, bool compactParty, BattleCombatant? selectedActor, bool canAttack)
    {
        if (party.Count <= 4)
        {
            foreach ((BattleCombatant actor, BattleHudPartyRow row) in VisiblePartyRows(party))
            {
                DrawPartyMember(canvas, theme, actor, row, compactParty, selectedActor, canAttack);
            }
        }
    }

    private void DrawPartyMember(IUiCanvas canvas, UiTheme theme, BattleCombatant actor, BattleHudPartyRow row, bool compactParty, BattleCombatant? selectedActor, bool canAttack, UiRect expandedRow = default, PartyMemberDisplay? display = null)
    {
        BattleSession session = controller.Session;
        display ??= ReadPartyMemberDisplay(actor);
        BattleGuardianForceCast? cast = display.Cast;
        BattleGuardianFormState? form = display.Form;
        string label = display.Name;
        BattlePresentedVital presented = display.Vital;
        int hp = presented.HitPoints;
        int maximumHp = presented.MaximumHitPoints;
        bool shownAlive = hp > 0;
        float gauge = cast is null ? actor.Atb / (float)BattleSession.AtbMaximum : cast.InitialTicks == 0 ? 1 : 1 - cast.RemainingTicks / (float)cast.InitialTicks;
        BattleVitalAppearance appearance = session.ReadVitalAppearance(actor.Id, cast?.Command.ActionId);
        UiColor nameColor = appearance.NameKnockedOut ? new(148, 148, 164) : new(238, 238, 238);
        UiColor hpColor = appearance.HitPointsKnockedOut ? new(255, 41, 24) : appearance.Critical ? new(255, 255, 123) : new(255, 255, 255);
        if (canAttack && selectedActor?.Id == actor.Id)
        {
            int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate % 16);
            int shade = ((phase < 8 ? phase : 15 - phase) + 2) << 4;
            // Native name/HP glyphs share a 16-frame triangle; 128 is neutral texture modulation.
            nameColor = BattleHudLayout.Modulate(nameColor, shade);
            hpColor = BattleHudLayout.Modulate(hpColor, shade);
        }
        BattleStatusPresentation statuses = display.Statuses;
        bool showTimer = form is { Phase: BattleGuardianFormPhase.Active };
        bool ready = cast is null && actor.IsReady;
        AtbVisualStatus gaugeStatus = (AtbVisualStatus)((statuses.Battle >> 1) & 7);
        if (expandedRow.IsEmpty)
        {
            Layout.DrawVitals(canvas, label, hp, maximumHp, row.Top, controller.TextScale, nameColor, hpColor, compactHeight: compactParty ? row.VitalHeight : null);
            DrawGauge(canvas, Layout.AtbGauge(row.Top, row.GaugeOffset), gauge, ready, shownAlive, cast is not null, gaugeStatus);
            if (compactParty)
            {
                canvas.PushClip(Rect(520, row.Top + row.StatusOffset, 414, row.Height - row.StatusOffset));
            }
            DrawStatuses(canvas, theme, statuses, 520, row.Top + row.StatusOffset, compactParty && showTimer ? 382 : 414, row.StatusTextScale);
            if (compactParty)
            {
                canvas.PopClip();
            }
        }
        else
        {
            UiRect gaugeBounds = DrawExpandedVitals(canvas, expandedRow, label, hp, maximumHp, nameColor, hpColor);
            DrawGauge(canvas, gaugeBounds, gauge, ready, shownAlive, cast is not null, gaugeStatus);
            DrawStatuses(canvas, theme, statuses, expandedRow.X + 8, row.Top + row.StatusOffset, expandedRow.Width - (showTimer ? 68 : 16), row.StatusTextScale);
        }
        if (showTimer)
        {
            string timer = controller.FormatSystemText("battle.hud.remaining-seconds", "{0}s", controller.GuardianFormSeconds(form!.RemainingTicks));
            if (!expandedRow.IsEmpty)
            {
                canvas.AlignedText(timer, 11 * row.StatusTextScale * Scale, Rect(expandedRow.Right - 60, row.Top + row.StatusOffset, 52, row.Height - row.StatusOffset), theme.Foreground, UiAlignment.End);
            }
            else if (compactParty)
            {
                canvas.Text(timer, 11 * row.StatusTextScale * Scale, Rect(906, row.Top + row.StatusOffset, 28, row.Height - row.StatusOffset), theme.Foreground);
            }
            else
            {
                Text(canvas, theme, timer, 906, row.Top - 5, 42, 18, 12);
            }
        }
    }

    private sealed record PartyMemberDisplay(BattleGuardianForceCast? Cast, BattleGuardianFormState? Form, string Name, BattlePresentedVital Vital, BattleStatusPresentation Statuses);

    private PartyMemberDisplay ReadPartyMemberDisplay(BattleCombatant actor)
    {
        BattleSession session = controller.Session;
        BattleGuardianForceCast? cast = session.GuardianForceCasts.FirstOrDefault(cast => cast.ActorId == actor.Id);
        BattleGuardianFormState? form = session.GetGuardianForm(actor.Id);
        int? guardian = cast?.Command.ActionId ?? form?.GuardianForceId;
        string name = guardian is int id ? controller.GuardianForceDisplayName(id) : actor.Name;
        return new(cast, form, name, session.ReadDisplayedVital(actor.Id, cast?.Command.ActionId), session.ReadStatusPresentation(actor.Id));
    }

    private void DrawExpandedParty(IUiCanvas canvas, UiTheme theme, IReadOnlyList<BattleCombatant> party, BattleCombatant? selectedActor, bool canAttack)
    {
        string[] ids = party.Select(actor => actor.Id).ToArray();
        bool sameParty = drawnPartyIds.SequenceEqual(ids);
        string? selectedId = selectedActor?.Id;
        bool sameLayout = drawnPartyLayout == (Bounds, controller.TextScale);
        int selectedIndex = sameParty && sameLayout && lastDrawnPartySelection == selectedId ? -1 : Array.IndexOf(ids, selectedId);
        PartyMemberDisplay[] displays = party.Select(ReadPartyMemberDisplay).ToArray();
        BattleHudPartyContent[] content = displays.Select(display => new BattleHudPartyContent(display.Name, display.Vital.HitPoints.ToString(CultureInfo.InvariantCulture), display.Vital.MaximumHitPoints.ToString(CultureInfo.InvariantCulture), MeasureStatusWidth(canvas, Scale, ReadStatusBadges(display.Statuses), controller.TextScale))).ToArray();
        partyList = BattleHudPartyList.Measure(canvas, Layout, content, controller.TextScale, sameParty ? requestedPartyStart ?? partyList.StartRow : 0, selectedIndex);
        requestedPartyStart = null;
        lastDrawnPartySelection = selectedId;
        drawnPartyIds = ids;
        drawnPartyLayout = (Bounds, controller.TextScale);
        drewExpandedParty = true;

        UiRect panel = ScaleRect(partyList.Panel);
        canvas.Fill(panel, new UiColor(theme.BorderDark.R, theme.BorderDark.G, theme.BorderDark.B, 178));
        foreach (int index in partyList.VisibleIndices())
        {
            UiRect bounds = partyList.Row(index);
            BattleHudPartyRow row = new(bounds.Y, bounds.Height, partyList.VitalHeight, partyList.VitalHeight, controller.TextScale, 5);
            DrawPartyMember(canvas, theme, party[index], row, true, selectedActor, canAttack, bounds, displays[index]);
        }
        if (partyList.Scrolls)
        {
            UiRect previous = partyList.PreviousPage;
            UiRect next = partyList.NextPage;
            canvas.AlignedText("▲", 16 * Scale, ScaleRect(previous), partyList.StartRow == 0 ? theme.Muted : theme.Foreground, UiAlignment.Center);
            canvas.AlignedText("▼", 16 * Scale, ScaleRect(next), partyList.EndRow >= partyList.Rows ? theme.Muted : theme.Foreground, UiAlignment.Center);
            string range = controller.FormatSystemText("battle.hud.party-range", "{0}–{1} / {2}", partyList.StartRow + 1, partyList.EndRow, partyList.Rows);
            canvas.AlignedText(range, 16 * Scale, Rect(previous.Right, previous.Y, next.X - previous.Right, previous.Height), theme.Foreground, UiAlignment.Center);
        }
    }

    private UiRect DrawExpandedVitals(IUiCanvas canvas, UiRect row, string name, int hp, int maximumHp, UiColor nameColor, UiColor hpColor)
    {
        float height = partyList.VitalLineHeight;
        float font = partyList.FontSize * Scale;
        float gaugeWidth = partyList.GaugeWidth;
        float gaugeX = row.Right - 8 - gaugeWidth;
        string current = hp.ToString(CultureInfo.InvariantCulture);
        string maximum = maximumHp.ToString(CultureInfo.InvariantCulture);
        float currentWidth = canvas.MeasureText(current, font, float.PositiveInfinity).Width / Scale + 2;
        float maximumWidth = canvas.MeasureText(maximum, font, float.PositiveInfinity).Width / Scale + 2;
        float maximumX = (partyList.StackedVitals ? row.Right - 8 : gaugeX - 10) - maximumWidth;
        float separatorX = maximumX - 14;
        float currentX = separatorX - currentWidth;
        float textRight = partyList.StackedVitals ? gaugeX : currentX;
        UiRect nameBounds = Rect(row.X + 8, row.Y, Math.Max(0, textRight - row.X - 14), height);
        canvas.Text(FitLabel(canvas, name, font, nameBounds.Width), font, nameBounds, nameColor, UiAlignment.Start, UiTextFlow.SingleLine);
        float vitalY = partyList.StackedVitals ? row.Y + height : row.Y;
        canvas.Text(current, font, Rect(currentX, vitalY, currentWidth, height), hpColor);
        canvas.AlignedText("/", font, Rect(separatorX, vitalY, 14, height), new UiColor(255, 255, 255), UiAlignment.Center);
        canvas.Text(maximum, font, Rect(maximumX, vitalY, maximumWidth, height), new UiColor(255, 255, 255));
        return Rect(gaugeX, row.Y + (height - 12) / 2, gaugeWidth, 12);
    }

    private static string FitLabel(IUiCanvas canvas, string name, float font, float width)
    {
        if (canvas.MeasureText(name, font, float.PositiveInfinity).Width <= width)
        {
            return name;
        }
        int[] characters = StringInfo.ParseCombiningCharacters(name);
        for (int index = characters.Length - 1; index > 0; index--)
        {
            string shortened = name[..characters[index]] + "…";
            if (canvas.MeasureText(shortened, font, float.PositiveInfinity).Width <= width)
            {
                return shortened;
            }
        }
        return "…";
    }

    private UiRect ScaleRect(UiRect rectangle) => Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private IEnumerable<(BattleCombatant Actor, BattleHudPartyRow Row)> VisiblePartyRows(IReadOnlyList<BattleCombatant> party)
    {
        if (controller.CommandMenuHidden)
        {
            yield break;
        }
        NativeBattleGraphicsState? graphics = controller.Session.NativeGraphics;
        NativeBattleUiWindows? windows = null;
        if (graphics?.UiServices is { } nativeUi)
        {
            windows = nativeUi.Windows ?? throw new NotSupportedException("Native battle HUD requires its retained UI window bank; migrate the named snapshot explicitly.");
            if (!windows.DrawsVitals4B1740(nativeUi.Flags942))
            {
                yield break;
            }
        }
        for (int index = 0; index < party.Count; index++)
        {
            BattleCombatant actor = party[index];
            if (windows is null || actor.NativeActorIndex is int originalSlot && windows.IsVitalsRowVisible4B1740(originalSlot))
            {
                yield return (actor, BattleHudLayout.PartyRow(index, party.Count, controller.TextScale));
            }
        }
    }

    private void DrawTargets(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleCombatant> targets = controller.VisibleTargets;
        bool group = controller.SelectingTargetGroup;
        string? selectedTargetId = controller.SelectedTarget?.Id;
        DrawTargetModelPointers(canvas, theme, targets, group, selectedTargetId);
        string action = controller.TargetActionCaption;
        string help = controller.CanConfirmMagicTargets ? controller.MagicConfirmCaption : group ? controller.TargetGroupDescription : controller.PhysicalOverrideCaption;
        bool expanded = targets.Count > 4;
        float maximumWidth = expanded ? Layout.ViewportRight - 54 - 26 : 442;
        float actionFont = 20 * Scale * controller.TextScale;
        float helpFont = 15 * Scale * controller.TextScale;
        float helpPanelWidth = 440;
        if (expanded)
        {
            float naturalWidth = Math.Max(canvas.MeasureText(action, actionFont, float.PositiveInfinity).Width, canvas.MeasureText(help, helpFont, float.PositiveInfinity).Width) / Scale;
            helpPanelWidth = Math.Clamp(naturalWidth + 28, 440, maximumWidth);
        }
        float measuredWidth = expanded ? (helpPanelWidth - 28) * Scale : float.PositiveInfinity;
        float actionHeight = canvas.MeasureText(expanded ? action : "Mg", actionFont, measuredWidth).Height / Scale;
        float helpHeight = string.IsNullOrEmpty(help) ? 0 : canvas.MeasureText(expanded ? help : "Mg", helpFont, measuredWidth).Height / Scale + 4;
        float helpPanelHeight = 14 + actionHeight + helpHeight;
        float helpGap = Math.Max(18, UiTheme.DefaultCaptionFontSize * controller.TextScale * 0.75f + 8);
        string[] ids = targets.Select(actor => actor.Id).ToArray();
        string[] names = targets.Select(controller.CombatantDisplayName).ToArray();
        bool sameTargets = drawnTargetIds.SequenceEqual(ids);
        int previousStart = sameTargets ? requestedTargetListStart ?? targetList.StartRow : 0;
        bool revealSelection = !group || !sameTargets || requestedTargetListStart is null && lastDrawnTargetId != selectedTargetId;
        targetList = Layout.MeasureTargetList(canvas, theme, names, controller.TextScale, helpPanelHeight + helpGap, maximumWidth, previousStart, revealSelection ? Array.IndexOf(ids, selectedTargetId) : -1);
        requestedTargetListStart = null;
        lastDrawnTargetId = selectedTargetId;
        drawnTargetIds = ids;
        drawnTargetLayout = (Bounds, controller.TextScale);
        UiRect panel = targetList.Panel;
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        string caption = controller.CanChangeTargetSide ? controller.SelectedTarget?.IsPlayer == true ? controller.SystemText("battle.hud.target-allies", "TARGET  < ALLIES >") : controller.SystemText("battle.hud.target-enemies", "TARGET  < ENEMIES >") : controller.SystemText("battle.hud.target", "TARGET");
        theme.Panel(canvas, Rect(panel.X, panel.Y, panel.Width, panel.Height), caption, captionSize);
        foreach (int index in targetList.VisibleIndices())
        {
            BattleCombatant actor = targets[index];
            UiRect row = targetList.Cell(index);
            float statusOffset = targetList.Expanded ? row.Height - targetList.StatusHeight : 25;
            UiRect text = Rect(row.X + 30, row.Y, row.Width - 42, targetList.Expanded ? statusOffset : row.Height);
            if (group || selectedTargetId == actor.Id)
            {
                if (targetList.Expanded)
                {
                    Layout.DrawCommandPointer(canvas, theme, row.X, row.Y);
                }
                else
                {
                    theme.Pointer(canvas, Left + (row.X - 48) * Scale, Top + (row.Y + 5) * Scale, 24 * Scale);
                }
            }
            canvas.PushClip(text);
            try
            {
                float font = 24 * Scale * controller.TextScale;
                canvas.Text(names[index], font, text, theme.Foreground, UiAlignment.Start, targetList.Expanded ? UiTextFlow.Wrap : UiTextFlow.SingleLine);
            }
            finally
            {
                canvas.PopClip();
            }
            if (targetList.Expanded)
            {
                canvas.PushClip(Rect(row.X, row.Y, row.Width, row.Height));
            }
            try
            {
                DrawStatuses(canvas, theme, controller.Session.ReadStatusPresentation(actor.Id), row.X + 30, row.Y + statusOffset, row.Width - 42);
            }
            finally
            {
                if (targetList.Expanded)
                {
                    canvas.PopClip();
                }
            }
        }
        if (targetList.Scrolls)
        {
            UiRect previous = targetList.PreviousPage;
            UiRect next = targetList.NextPage;
            Text(canvas, theme, "▲", previous.X + 8, previous.Y, previous.Width - 8, previous.Height, 16, targetList.StartRow == 0);
            Text(canvas, theme, "▼", next.X + 8, next.Y, next.Width - 8, next.Height, 16, targetList.EndRow >= targetList.ContentRows);
            string range = controller.FormatSystemText("battle.hud.target-range", "{0}–{1} / {2}", targetList.StartRow + 1, targetList.EndRow, targetList.ContentRows);
            canvas.AlignedText(range, 16 * Scale, Rect(previous.Right, previous.Y, next.X - previous.Right, previous.Height), theme.Foreground, UiAlignment.Center);
        }
        float helpY = panel.Y - helpGap - helpPanelHeight;
        if (expanded)
        {
            helpPanelWidth = Math.Max(helpPanelWidth, panel.Width);
        }
        targetHelpBounds = Rect(54, helpY, helpPanelWidth, helpPanelHeight);
        theme.Panel(canvas, targetHelpBounds);
        Text(canvas, theme, action, 68, helpY + 7, helpPanelWidth - 28, actionHeight, 20);
        Text(canvas, theme, help, 68, helpY + 7 + actionHeight + 4, helpPanelWidth - 28, helpHeight, 15, true);
    }

    private void DrawTargetModelPointers(IUiCanvas canvas, UiTheme theme, IReadOnlyList<BattleCombatant> targets, bool group, string? selectedTargetId)
    {
        foreach (BattleCombatant target in targets)
        {
            if (!target.IsActive || target.Hidden || !group && selectedTargetId != target.Id)
            {
                continue;
            }
            DrawModelPointer(canvas, theme, Bounds, Scale, controller.TargetModelBounds?.Invoke(target.Id));
        }
    }

    internal static void DrawModelPointer(IUiCanvas canvas, UiTheme theme, UiRect viewport, float scale, UiRect? modelBounds)
    {
        if (modelBounds is not { IsEmpty: false } model || model.Right <= viewport.X || model.X >= viewport.Right || model.Bottom <= viewport.Y || model.Y >= viewport.Bottom)
        {
            return;
        }
        float size = 24 * scale;
        float width = size * 1.5f;
        float x = Math.Clamp(model.X - width - 4 * scale, viewport.X, Math.Max(viewport.X, viewport.Right - width));
        float y = Math.Clamp(model.Y + model.Height * 0.35f - size / 2, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - size));
        theme.Pointer(canvas, x, y, size);
    }

    internal static void DrawGauge(IUiCanvas canvas, UiRect bounds, float ratio, bool ready, bool alive, bool summoning, AtbVisualStatus statuses)
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
        if (key is UiKey.PageUp or UiKey.PageDown && controller.SelectingTarget && targetList.Scrolls && TargetListIsCurrent())
        {
            ScrollTargetPage(key == UiKey.PageUp ? -1 : 1);
            return true;
        }
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

    private bool TargetContains(UiRect bounds, float x, float y)
    {
        return Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height).Contains(x, y);
    }

    private void PointAtTarget(float x, float y, bool confirm)
    {
        if (!TargetListIsCurrent())
        {
            return;
        }
        int index = targetList.HitIndex(Layout, x, y);
        if (index >= 0 && index < drawnTargetIds.Length)
        {
            controller.SelectTarget(drawnTargetIds[index], confirm);
        }
    }

    private bool ClickTargetPage(float x, float y)
    {
        if (!targetList.Scrolls || !TargetListIsCurrent())
        {
            return false;
        }
        if (TargetContains(targetList.PreviousPage, x, y))
        {
            ScrollTargetPage(-1);
            return true;
        }
        if (TargetContains(targetList.NextPage, x, y))
        {
            ScrollTargetPage(1);
            return true;
        }
        return false;
    }

    private bool TargetListIsCurrent()
    {
        return drawnTargetLayout == (Bounds, controller.TextScale) && drawnTargetIds.SequenceEqual(controller.VisibleTargets.Select(target => target.Id));
    }

    private void ScrollTargetPage(int direction)
    {
        int row = Math.Clamp(targetList.StartRow + direction * targetList.VisibleRows, 0, targetList.ContentRows - targetList.VisibleRows);
        if (row == targetList.StartRow)
        {
            return;
        }
        requestedTargetListStart = row;
        if (!controller.SelectingTargetGroup)
        {
            int selected = Array.IndexOf(drawnTargetIds, controller.SelectedTarget?.Id);
            int column = targetList.Grid.GetCell(selected).Column;
            while (column > 0 && targetList.Grid.ColumnRows(column) <= row)
            {
                column--;
            }
            int index = targetList.Grid.GetIndex(column, row);
            if (index >= 0)
            {
                controller.SelectTarget(drawnTargetIds[index], false);
            }
        }
    }

    public override void PointerHover(float x, float y)
    {
        if (controller.CardSlotsOwnsInput)
        {
            controller.SlotsPointer(x, y, Bounds, false);
            return;
        }
        if (!controller.CommandMenuHidden && controller.ShowingDrawList)
        {
            HoverDrawList(x, y);
            return;
        }
        if (!controller.CommandMenuHidden && controller.ShowingMagicList)
        {
            HoverMagicList(x, y);
            return;
        }
        if (controller.CommandMenuHidden || !controller.SelectingTarget)
        {
            return;
        }
        PointAtTarget(x, y, false);
    }

    public override void Pointer(float x, float y)
    {
        if (controller.CardSlotsOwnsInput)
        {
            controller.SlotsPointer(x, y, Bounds, true);
            return;
        }
        if (controller.NativeShotState is not null)
        {
            if (Rect(36, 570, 354, 70).Contains(x, y))
            {
                controller.FireNativeShot();
            }
            else if (Rect(408, 570, 354, 70).Contains(x, y))
            {
                controller.Navigate(MenuInput.Cancel);
            }
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
        if (controller.ShowingDrawList)
        {
            ClickDrawList(x, y);
            return;
        }
        if (controller.ShowingMagicList)
        {
            ClickMagicList(x, y);
            return;
        }
        if (controller.CanChangeTargetSide && TargetListIsCurrent() && TargetContains(targetList.Caption, x, y))
        {
            controller.ChangeTargetSide();
            return;
        }
        if (controller.SelectingTarget && TargetContains(targetList.Panel, x, y) || !controller.SelectingTarget && Rect(30, CommandPanelY, CommandWidth, Math.Max(132, CommandPanelHeight)).Contains(x, y))
        {
            if (controller.Session.Outcome != BattleOutcome.Active)
            {
                controller.Complete();
            }
            else if (controller.SelectingTarget)
            {
                if (!ClickTargetPage(x, y))
                {
                    PointAtTarget(x, y, true);
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
        else if (controller.Session.Outcome == BattleOutcome.Active && controller.NativeDuelState is null)
        {
            IReadOnlyList<BattleCombatant> party = controller.Party;
            if (party.Count > 4)
            {
                ClickExpandedParty(x, y);
                return;
            }
            foreach ((BattleCombatant actor, BattleHudPartyRow row) in VisiblePartyRows(party))
            {
                if (Layout.PartyRowBounds(row).Contains(x, y))
                {
                    controller.SelectParty(actor.Id);
                    return;
                }
            }
        }
    }

    private void ClickExpandedParty(float x, float y)
    {
        if (!drewExpandedParty || drawnPartyLayout != (Bounds, controller.TextScale) || controller.SelectingTarget && targetHelpBounds.Contains(x, y))
        {
            return;
        }
        if (partyList.Scrolls)
        {
            if (ScaleRect(partyList.PreviousPage).Contains(x, y))
            {
                requestedPartyStart = Math.Max(0, partyList.StartRow - partyList.VisibleRows);
                return;
            }
            if (ScaleRect(partyList.NextPage).Contains(x, y))
            {
                requestedPartyStart = Math.Min(partyList.Rows - partyList.VisibleRows, partyList.StartRow + partyList.VisibleRows);
                return;
            }
        }
        foreach (int index in partyList.VisibleIndices())
        {
            if (ScaleRect(partyList.Row(index)).Contains(x, y))
            {
                controller.SelectParty(drawnPartyIds[index]);
                return;
            }
        }
    }
}
