using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Procedural FF8-style command panels with a shared initiative rail above the battle scene.</summary>
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
        if (controller.ShowInitiative)
        {
            DrawTimeline(canvas, theme);
        }
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

    private void DrawTimeline(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, Rect(16, 14, 928, 116));
        Text(canvas, theme, "INITIATIVE · estimated time to ready", 30, 23, 506, 22, 15);
        Text(canvas, theme, controller.Session.Mode == BattleAtbMode.TurnBased ? "TURN BASED" : "NORMAL ATB", 746, 23, 178, 22, 15);
        canvas.Fill(Rect(72, 109, 818, 4), new UiColor(27, 27, 30));
        canvas.Fill(Rect(776, 109, 114, 4), new UiColor(184, 166, 111));
        var estimates = controller.Session.Combatants.Where(actor => actor.IsAlive).Select(actor => new { Actor = actor, Frames = controller.Session.EstimatedReadyFrames(actor.Id) ?? 0 }).OrderByDescending(item => item.Frames).ThenBy(item => item.Actor.Id, StringComparer.Ordinal).ToArray();
        int horizon = Math.Max(60, (int)Math.Ceiling(estimates.Select(item => item.Frames).DefaultIfEmpty(0).Max() / 60.0) * 60);
        Text(canvas, theme, $"{horizon / BattleSession.FramesPerSecond}s", 30, 107, 44, 22, 12);
        Text(canvas, theme, "NOW", 898, 107, 42, 22, 12);
        float[] positions = new float[estimates.Length];
        for (int index = 0; index < estimates.Length; index++)
        {
            float desired = 74 + (1 - estimates[index].Frames / (float)horizon) * 770;
            positions[index] = index == 0 ? desired : Math.Max(desired, positions[index - 1] + 48);
        }
        if (positions.Length > 0)
        {
            positions[^1] = Math.Min(positions[^1], 850);
            for (int index = positions.Length - 2; index >= 0; index--)
            {
                positions[index] = Math.Min(positions[index], positions[index + 1] - 48);
            }
        }
        for (int index = 0; index < estimates.Length; index++)
        {
            BattleCombatant actor = estimates[index].Actor;
            float x = positions[index];
            float railX = 74 + (1 - estimates[index].Frames / (float)horizon) * 770;
            string name = actor.Name.Length <= 7 ? actor.Name : actor.Name[..4] + "…" + actor.Name[^2..];
            Text(canvas, theme, name, x - 3, 44, 46, 17, 11);
            UiRect portrait = Rect(x, 61, 36, 36);
            canvas.Fill(new UiRect(portrait.X - 2 * Scale, portrait.Y - 2 * Scale, portrait.Width + 4 * Scale, portrait.Height + 4 * Scale), actor.IsPlayer ? new UiColor(124, 169, 220) : new UiColor(197, 110, 96));
            if (controller.HeadRenderer is not null)
            {
                controller.HeadRenderer(actor, canvas, portrait);
            }
            else
            {
                var fallback = new UiPortrait { Seed = actor.ModelId, Initials = actor.Name.Length == 0 ? "?" : actor.Name[..1] };
                fallback.Arrange(portrait);
                fallback.Draw(canvas, theme);
            }
            canvas.Line(portrait.X + portrait.Width / 2, portrait.Bottom, Left + (railX + 18) * Scale, Top + 111 * Scale, actor.IsPlayer ? new UiColor(124, 169, 220) : new UiColor(197, 110, 96), Scale);
            string state = controller.Session.ActiveAction?.ActorId == actor.Id ? "ACT" : actor.IsBusy ? "QUEUE" : actor.IsReady ? "READY" : "";
            if (state.Length > 0)
            {
                canvas.Fill(Rect(x - 3, 94, 46, 13), new UiColor(38, 38, 43));
                Text(canvas, theme, state, x - 1, 94, 44, 16, 9);
            }
            string time = actor.IsReady ? "NOW" : $"{estimates[index].Frames / (float)BattleSession.FramesPerSecond:0.0}s";
            canvas.Fill(Rect(x - 3, 114, 46, 14), new UiColor(38, 38, 43));
            Text(canvas, theme, time, x - 1, 114, 44, 16, 10);
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
