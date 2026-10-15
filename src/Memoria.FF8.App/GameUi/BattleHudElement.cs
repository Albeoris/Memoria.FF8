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
        theme.Panel(canvas, Rect(16, 494, 252, 130));
        theme.Panel(canvas, Rect(280, 494, 664, 130));
        Text(canvas, theme, session.Outcome != BattleOutcome.Active ? "RESULT" : controller.SelectedActor?.Name ?? "COMMAND", 34, 508, 220, 26, 20);
        bool canAttack = controller.SelectedActor is not null && session.Outcome == BattleOutcome.Active;
        if (session.Outcome != BattleOutcome.Active)
        {
            theme.Pointer(canvas, Left + 32 * Scale, Top + 554 * Scale, 12 * Scale);
            Text(canvas, theme, session.Outcome == BattleOutcome.Victory ? "Continue" : "Load / Retry", 56, 546, 186, 30, 24);
            Text(canvas, theme, "Confirm / Click", 34, 588, 220, 26, 15);
        }
        else if (controller.SelectingTarget)
        {
            DrawTargets(canvas, theme);
        }
        else
        {
            int start = controller.ChoiceStart;
            for (int row = 0; row < 3 && start + row < controller.ChoiceLabels.Count; row++)
            {
                float y = 544 + row * 24;
                if (start + row == controller.ChoiceIndex)
                {
                    theme.Pointer(canvas, Left + 32 * Scale, Top + (y + 5) * Scale, 10 * Scale);
                }
                Text(canvas, theme, controller.ChoiceLabels[start + row], 52, y, 198, 24, 17, !canAttack);
            }
            if (!canAttack)
            {
                Text(canvas, theme, "Waiting for ATB", 34, 588, 220, 26, 15, true);
            }
        }

        IReadOnlyList<BattleCombatant> party = controller.Party;
        for (int index = 0; index < party.Count; index++)
        {
            BattleCombatant actor = party[index];
            float y = 508 + index * 35;
            if (controller.SelectedActor?.Id == actor.Id)
            {
                theme.Pointer(canvas, Left + 294 * Scale, Top + (y + 8) * Scale, 10 * Scale);
            }
            Text(canvas, theme, actor.Name, 314, y, 190, 27, 20, !actor.IsAlive);
            Text(canvas, theme, $"{actor.CurrentHp,4} / {actor.MaximumHp,4}", 508, y, 228, 27, 20, !actor.IsAlive);
            DrawGauge(canvas, Rect(746, y + 6, 176, 15), actor.Atb / (float)BattleSession.AtbMaximum, actor.IsReady, actor.IsAlive);
        }
        theme.Panel(canvas, Rect(16, 634, 928, 30));
        string message = session.Outcome == BattleOutcome.Active ? controller.Message : session.Outcome.ToString().ToUpperInvariant();
        Text(canvas, theme, message, 28, 639, 904, 24, 15);
        theme.Panel(canvas, Rect(794, 444, 150, 36));
        Text(canvas, theme, "Debug Battle", 808, 450, 128, 28, 16);
        if (session.Mode == BattleAtbMode.TurnBased)
        {
            theme.Panel(canvas, Rect(568, 444, 214, 36));
            Text(canvas, theme, "Advance Turn  [Space]", 582, 450, 196, 28, 16);
        }
    }

    private void DrawTargets(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleCombatant> targets = controller.VisibleTargets;
        for (int index = 0; index < targets.Count && index < 3; index++)
        {
            BattleCombatant actor = targets[index];
            float y = 544 + index * 24;
            if (controller.SelectedTarget?.Id == actor.Id)
            {
                theme.Pointer(canvas, Left + 32 * Scale, Top + (y + 5) * Scale, 10 * Scale);
            }
            Text(canvas, theme, actor.Name, 52, y, 198, 24, 17);
        }
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
        if (Rect(794, 444, 150, 36).Contains(x, y))
        {
            controller.OpenDebug();
        }
        else if (controller.Session.Mode == BattleAtbMode.TurnBased && Rect(568, 444, 214, 36).Contains(x, y))
        {
            controller.AdvanceTurn();
        }
        else if (Rect(16, 494, 252, 130).Contains(x, y))
        {
            if (controller.Session.Outcome != BattleOutcome.Active)
            {
                controller.Complete();
            }
            else if (controller.SelectingTarget)
            {
                int index = (int)((y - Rect(0, 544, 0, 0).Y) / (24 * Scale));
                if (index >= 0 && index < controller.VisibleTargets.Count)
                {
                    controller.SelectTarget(controller.VisibleTargets[index].Id, true);
                }
            }
            else
            {
                int index = (int)((y - Rect(0, 544, 0, 0).Y) / (24 * Scale)) + controller.ChoiceStart;
                if (index >= 0 && index < controller.ChoiceLabels.Count)
                {
                    controller.Choose(index);
                }
            }
        }
        else if (Rect(280, 494, 664, 130).Contains(x, y))
        {
            int index = (int)((y - Rect(0, 508, 0, 0).Y) / (35 * Scale));
            if (index >= 0 && index < controller.Party.Count)
            {
                controller.SelectParty(controller.Party[index].Id);
            }
        }
    }
}
