using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Procedural FF8-style command panels and party ATB gauges.</summary>
internal sealed class BattleHudElement : UiElement
{
    private readonly BattleHudController controller;
    private float Scale => Math.Min(Bounds.Width / 960, Bounds.Height / 672);
    private float Left => Bounds.X + (Bounds.Width - 960 * Scale) / 2;
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

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        BattleSession session = controller.Session;
        if (controller.BattleMessageDocument is { } battleMessage && session.Outcome == BattleOutcome.Active)
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
        bool canAttack = controller.SelectedActor is not null && session.Outcome == BattleOutcome.Active;
        IReadOnlyList<string> choices = controller.ChoiceLabels;
        string caption = session.Outcome != BattleOutcome.Active ? "RESULT" : controller.SelectingTarget ? "TARGET" : controller.SelectingDrawMode ? "DRAW" : controller.SelectingAction ? controller.SelectedCommand.ToString().ToUpperInvariant() : "COMMAND";
        int rows = controller.SelectingTarget ? controller.VisibleTargets.Count : choices.Count;
        float menuHeight = session.Outcome != BattleOutcome.Active ? 76 : 20 + Math.Max(1, Math.Min(4, rows)) * 28;
        if (canAttack || session.Outcome != BattleOutcome.Active)
        {
            theme.Panel(canvas, Rect(30, 516, 194, menuHeight), caption, 17 * Scale * controller.TextScale);
        }
        if (session.Outcome != BattleOutcome.Active)
        {
            theme.Pointer(canvas, Left + 38 * Scale, Top + 540 * Scale, 12 * Scale);
            Text(canvas, theme, session.CanReturnToField ? "Continue" : "Load / Retry", 60, 532, 156, 30, 21);
        }
        else if (controller.SelectingTarget)
        {
            DrawTargets(canvas, theme);
        }
        else if (canAttack)
        {
            int start = Math.Clamp(controller.ChoiceIndex - 1, 0, Math.Max(0, choices.Count - 4));
            IReadOnlyList<BattleActionDefinition> actions = controller.SelectingAction && !controller.SelectingDrawMode ? controller.ActionChoices : [];
            for (int row = 0; row < 4 && start + row < choices.Count; row++)
            {
                float y = 530 + row * 28;
                if (start + row == controller.ChoiceIndex)
                {
                    theme.Pointer(canvas, Left + 38 * Scale, Top + (y + 5) * Scale, 10 * Scale);
                }
                bool unavailable = actions.Count > start + row && !actions[start + row].Available;
                Text(canvas, theme, choices[start + row], 60, y, 156, 26, 20, unavailable);
            }
        }

        if (canAttack && (controller.SelectingAction || controller.SelectingTarget) && controller.BattleMessageDocument is null)
        {
            Text(canvas, theme, controller.CommandHelpMessage, 30, 468, 440, 42, 15);
        }

        IReadOnlyList<BattleCombatant> party = controller.Party;
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
            string label = gf is null ? actor.Name : controller.GuardianForceDisplayName(gf.Id);
            BattlePresentedVital presented = session.ReadPresentedVital(actor.Id, gf?.Id);
            int hp = presented.HitPoints;
            int maximumHp = presented.MaximumHitPoints;
            bool shownAlive = hp > 0;
            float gauge = cast is null ? actor.Atb / (float)BattleSession.AtbMaximum : cast.InitialTicks == 0 ? 1 : 1 - cast.RemainingTicks / (float)cast.InitialTicks;
            Text(canvas, theme, label, 520, y, 150, 30, 22, !shownAlive);
            Text(canvas, theme, $"{hp,4} / {maximumHp,4}", 674, y, 150, 30, 22, !shownAlive);
            DrawGauge(canvas, Rect(830, y + 11, 104, 12), gauge, cast is null && actor.IsReady, shownAlive);
        }
        if (session.Outcome == BattleOutcome.Victory)
        {
            DrawRewards(canvas, theme);
        }
        if (session.GunbladeTriggerWindowOpen)
        {
            Text(canvas, theme, $"Gunblade: {controller.GunbladeBindingHint}", 368, 112, 280, 30, 20);
        }
        if (session.ActiveAction is { Phase: BattleActionPhase.Recovery } action && session.Events.Any(item => item.Kind == "GunbladeTrigger" && item.ActorId == action.ActorId && session.Frame - item.Frame < BattleSession.AttackRecoveryFrames))
        {
            Text(canvas, theme, "TRIGGER!", 372, 100, 280, 48, 30);
        }
        if (session.Mode == BattleAtbMode.TurnBased)
        {
            Text(canvas, theme, "Advance Turn  [Space]", 706, 640, 228, 26, 16);
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

    private void DrawRewards(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, Rect(120, 28, 720, 404));
        Text(canvas, theme, "VICTORY · REWARDS", 144, 46, 672, 34, 24);
        IReadOnlyList<string> lines = controller.VisibleRewardLines;
        for (int index = 0; index < lines.Count; index++)
        {
            Text(canvas, theme, lines[index], 144, 98 + index * 32, 672, 30, 20);
        }
        string help = controller.RewardPageCount > 1 ? $"< Previous     {controller.RewardPage + 1} / {controller.RewardPageCount}     Next >   [Left / Right]" : "Confirm / Continue to return";
        Text(canvas, theme, help, 144, 382, 672, 28, 16);
    }

    private static void DrawGauge(IUiCanvas canvas, UiRect bounds, float ratio, bool ready, bool alive)
    {
        canvas.Fill(bounds, new UiColor(22, 22, 27));
        UiRect inner = bounds.Inset(2);
        UiColor color = !alive ? new UiColor(75, 75, 80) : ready ? new UiColor(237, 211, 136) : new UiColor(169, 185, 204);
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

    public override void Pointer(float x, float y)
    {
        if (controller.Session.Outcome == BattleOutcome.Victory && Rect(144, 376, 672, 40).Contains(x, y))
        {
            controller.ChangeRewardPage(x < Rect(480, 0, 0, 0).X ? -1 : 1);
        }
        else if (controller.Session.Mode == BattleAtbMode.TurnBased && Rect(706, 640, 228, 26).Contains(x, y))
        {
            controller.AdvanceTurn();
        }
        else if (Rect(30, 516, 194, 132).Contains(x, y))
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
                    controller.Choose(index);
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
