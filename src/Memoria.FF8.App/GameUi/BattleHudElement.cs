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

    private void Text(IUiCanvas canvas, UiTheme theme, string value, float x, float y, float width, float height, float size = 18, bool muted = false)
    {
        canvas.Text(value, size * Scale * controller.TextScale, Rect(x, y, width, height), muted ? theme.Muted : theme.Foreground);
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
        BattleSession session = controller.Session;
        if (session.Outcome == BattleOutcome.Victory)
        {
            DrawResults(canvas, theme);
            if (controller.ResultOpacity < 1)
            {
                canvas.Fill(Bounds, new UiColor(0, 0, 0, (byte)(255 * (1 - controller.ResultOpacity))));
            }
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
        string caption = session.Outcome != BattleOutcome.Active ? "RESULT" : controller.SelectingTarget ? "TARGET" : controller.SelectingGuardianFormAction ? "GF ACTION" : controller.SelectingDrawMode ? "DRAW" : controller.SelectingAction ? controller.SelectedCommand.ToString().ToUpperInvariant() : "COMMAND";
        IReadOnlyList<BattleActionDefinition> actions = controller.SelectingAction && !controller.SelectingDrawMode && !controller.SelectingGuardianFormAction && !controller.HasGuardianFormCommands ? controller.ActionChoices : [];
        string[] values = actions.Select(controller.ChoiceValue).ToArray();
        float fontSize = (ShowingMagicList ? 18 : 20) * controller.TextScale;
        float valueWidth = 0;
        float ownerWidth = ShowingMagicList && actions.Any(action => controller.MagicOwnerIcon(action).HasValue) ? 30 : 0;
        if (actions.Count > 0)
        {
            float nameWidth = choices.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            valueWidth = values.Select(value => canvas.MeasureText(value, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            float fixedWidth = 38 + ownerWidth + (valueWidth > 0 ? 12 : 0);
            float textWidth = nameWidth + valueWidth;
            float fit = Math.Min(1, Math.Min(24 / fontSize, (440 - fixedWidth) / Math.Max(1, textWidth)));
            fontSize *= fit;
            valueWidth *= fit;
            CommandWidth = Math.Clamp(fixedWidth + textWidth * fit, 194, 440);
        }
        else
        {
            CommandWidth = 194;
        }
        int rows = controller.SelectingTarget ? controller.VisibleTargets.Count : choices.Count;
        float menuHeight = session.Outcome != BattleOutcome.Active ? 76 : 20 + Math.Max(1, Math.Min(4, rows)) * 28;
        if (canAttack || session.Outcome != BattleOutcome.Active)
        {
            theme.Panel(canvas, Rect(30, 516, CommandWidth, menuHeight), caption, 17 * Scale * controller.TextScale);
        }
        if (session.Outcome != BattleOutcome.Active)
        {
            theme.Pointer(canvas, Left + 38 * Scale, Top + 540 * Scale, 12 * Scale);
            Text(canvas, theme, session.CanReturnToField ? "Continue" : "Load / Retry", 60, 532, 156, 30, 21);
        }
        else if (!controller.CommandMenuHidden && controller.SelectingTarget)
        {
            theme.Panel(canvas, Rect(30, 452, 440, 58));
            Text(canvas, theme, controller.TargetActionCaption, 44, 459, 412, 24, 20);
            Text(canvas, theme, controller.PhysicalOverrideCaption, 44, 483, 412, 22, 15, true);
            DrawTargets(canvas, theme);
        }
        else if (canAttack)
        {
            int start = Math.Clamp(controller.ChoiceIndex - 1, 0, Math.Max(0, choices.Count - 4));
            for (int row = 0; row < 4 && start + row < choices.Count; row++)
            {
                float y = 530 + row * 28;
                if (start + row == controller.ChoiceIndex)
                {
                    theme.Pointer(canvas, Left + 38 * Scale, Top + (y + 5) * Scale, 10 * Scale);
                }
                bool unavailable = actions.Count > start + row && (!actions[start + row].Available || controller.SelectedCommand == BattleAbility.Magic && controller.SelectedActor is { } caster && !session.QuoteMagic(caster.Id, actions[start + row].Id).Allowed);
                float valueLeft = 30 + CommandWidth - 8 - valueWidth;
                float ownerLeft = valueLeft - (valueWidth > 0 ? 12 : 0) - ownerWidth;
                UiImageSource? ownerIcon = ShowingMagicList && actions.Count > start + row ? controller.MagicOwnerIcon(actions[start + row]) : null;
                if (ownerIcon is { } portrait)
                {
                    canvas.Image(portrait, Rect(ownerLeft, y, 26, 26));
                }
                ChoiceText(canvas, theme, choices[start + row], 60, y, Math.Max(0, ownerLeft - 60), fontSize / controller.TextScale, unavailable);
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
            float y = 528 + index * 38;
            if (controller.SelectedActor?.Id == actor.Id)
            {
                theme.Pointer(canvas, Left + 496 * Scale, Top + (y + 8) * Scale, 10 * Scale);
            }
            BattleGuardianForceCast? cast = session.GuardianForceCasts.FirstOrDefault(cast => cast.ActorId == actor.Id);
            BattleGuardianForceVital? gf = cast is null ? null : session.GuardianForces.Single(gf => gf.Id == cast.Command.ActionId);
            BattleGuardianFormState? form = session.GetGuardianForm(actor.Id);
            string label = gf is not null ? controller.GuardianForceDisplayName(gf.Id) : form is not null ? controller.GuardianForceDisplayName(form.GuardianForceId) : actor.Name;
            BattlePresentedVital presented = session.ReadDisplayedVital(actor.Id, gf?.Id);
            int hp = presented.HitPoints;
            int maximumHp = presented.MaximumHitPoints;
            bool shownAlive = hp > 0;
            float gauge = cast is null ? actor.Atb / (float)BattleSession.AtbMaximum : cast.InitialTicks == 0 ? 1 : 1 - cast.RemainingTicks / (float)cast.InitialTicks;
            Text(canvas, theme, label, 520, y, 150, 30, 22, !shownAlive);
            Text(canvas, theme, $"{hp,4} / {maximumHp,4}", 674, y, 150, 30, 22, !shownAlive);
            DrawGauge(canvas, Rect(830, y + 11, 104, 12), gauge, cast is null && actor.IsReady, shownAlive, cast is not null);
            if (form is { Phase: BattleGuardianFormPhase.Active })
            {
                Text(canvas, theme, $"{(form.RemainingTicks + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond}s", 906, y - 5, 42, 18, 12);
            }
        }
        if (session.ActiveAction is { Phase: BattleActionPhase.Recovery } action && session.Events.Any(item => item.Kind == "GunbladeTrigger" && item.ActorId == action.ActorId && session.Frame - item.Frame < BattleSession.AttackRecoveryFrames))
        {
            Text(canvas, theme, "TRIGGER!", 372, 100, 280, 48, 30);
        }
    }

    private void DrawTargets(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleCombatant> targets = controller.VisibleTargets;
        for (int index = 0; index < targets.Count && index < 3; index++)
        {
            BattleCombatant actor = targets[index];
            float y = 530 + index * 28;
            if (controller.SelectedTarget?.Id == actor.Id)
            {
                theme.Pointer(canvas, Left + 38 * Scale, Top + (y + 5) * Scale, 10 * Scale);
            }
            Text(canvas, theme, actor.Name, 60, y, 156, 26, 20);
        }
    }

    private static void DrawGauge(IUiCanvas canvas, UiRect bounds, float ratio, bool ready, bool alive, bool summoning)
    {
        canvas.Fill(bounds, new UiColor(22, 22, 27));
        UiRect inner = bounds.Inset(2);
        UiColor color = !alive ? new UiColor(75, 75, 80) : summoning ? new UiColor(64, 128, 255) : ready ? new UiColor(237, 211, 136) : new UiColor(169, 185, 204);
        canvas.Fill(new UiRect(inner.X, inner.Y, inner.Width * Math.Clamp(ratio, 0, 1), inner.Height), color);
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

    public override void PointerHover(float x, float y)
    {
        if (controller.CommandMenuHidden || !controller.SelectingTarget || !Rect(30, 530, 194, 84).Contains(x, y))
        {
            return;
        }
        int index = (int)Math.Floor((y - Rect(0, 530, 0, 0).Y) / (28 * Scale));
        if (index >= 0 && index < controller.VisibleTargets.Count)
        {
            controller.SelectTarget(controller.VisibleTargets[index].Id, false);
        }
    }

    public override void Pointer(float x, float y)
    {
        if (controller.CommandMenuHidden)
        {
            return;
        }
        if (controller.Session.Outcome == BattleOutcome.Victory)
        {
            if (ResultRect(16, 184, 352, 26).Contains(x, y))
            {
                controller.ConfirmResult();
            }
            return;
        }
        if (Rect(30, 516, CommandWidth, 132).Contains(x, y))
        {
            if (controller.Session.Outcome != BattleOutcome.Active)
            {
                controller.Complete();
            }
            else if (controller.SelectingTarget)
            {
                int index = (int)Math.Floor((y - Rect(0, 530, 0, 0).Y) / (28 * Scale));
                if (index >= 0 && index < controller.VisibleTargets.Count)
                {
                    controller.SelectTarget(controller.VisibleTargets[index].Id, true);
                }
            }
            else
            {
                int index = (int)Math.Floor((y - Rect(0, 530, 0, 0).Y) / (28 * Scale)) + controller.ChoiceStart;
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
