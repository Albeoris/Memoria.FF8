using System.Globalization;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly record struct BattleChoiceRow(string Name, string Value = "");

    private void DrawBattle(Rectangle bounds, Rectangle sceneBounds, Ff7BattleSession battle)
    {
        IUiCanvas canvas = dialogueCanvas ?? throw new InvalidOperationException("FFVII battle requires the shared host presentation context.");
        UiTheme theme = SharedTheme();
        BattleHudLayout layout = new(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        UiPreferences preferences = PresentationPreferences;
        float textScale = preferences.TextScale / 100f * preferences.UiFontSize / 24f;
        if (battle.ResultReady)
        {
            DrawBattleResult(canvas, theme, layout, textScale, battle);
            return;
        }

        DrawBattleParty(canvas, layout, textScale, battle);
        if (battle.BeatRushSelecting)
        {
            DrawBattleChoices(canvas, theme, layout, textScale, "BEAT RUSH", [new(battle.BeatRushVisible!.Value.ToString()), new("Confirm to stop")], 0);
        }
        else if (battle.ReadyPlayer is int ready)
        {
            Ff7BattleCommandOption[] commands = AvailableBattleCommands(battle, ready);
            if (selectedBattleCommand is { } selected && commands.FirstOrDefault(option => option.Command == selected.Command && option.Argument == selected.Argument) is { } current)
            {
                DrawBattleTargets(canvas, theme, layout, textScale, battle, ready, current);
            }
            else if (battleCommandPage is { } page)
            {
                bool guest = IsMixedBattleSlot(ready);
                BattleChoiceRow[] actions = commands.Where(option => option.Command == page).Select(option => new BattleChoiceRow(CommandName(option, ready), BattleCommandValue(option, guest))).ToArray();
                DrawBattleChoices(canvas, theme, layout, textScale, page.ToString().ToUpperInvariant(), actions, battleActionIndex, page == Ff7BattleCommand.Magic);
            }
            else
            {
                BattleChoiceRow[] categories = commands.GroupBy(option => option.Command).Select(group => new BattleChoiceRow(group.Key == Ff7BattleCommand.Limit && group.Count() == 1 ? CommandName(group.First(), ready) : group.Key.ToString())).ToArray();
                DrawBattleChoices(canvas, theme, layout, textScale, "COMMAND", categories, battleCommandIndex);
            }
        }
        DrawBattleMessage(canvas, theme, layout, textScale, battle);
        DrawBattleDamage(canvas, theme, layout.Scale * textScale, sceneBounds, battle);
    }

    internal static string BattleCommandValue(Ff7BattleCommandOption option, bool guest)
    {
        return option.Command switch
        {
            Ff7BattleCommand.Magic when !guest => option.Cost > 0 ? $"{option.Cost} MP" : "",
            Ff7BattleCommand.Magic or Ff7BattleCommand.Item => option.Cost.ToString(),
            _ => ""
        };
    }

    private static void DrawBattleParty(IUiCanvas canvas, BattleHudLayout layout, float textScale, Ff7BattleSession battle)
    {
        int row = 0;
        foreach (Ff7BattleActorState actor in battle.Actors.Where(actor => actor.Actor.Slot < 3))
        {
            int slot = actor.Actor.Slot;
            float y = BattleHudLayout.PartyTop + row * BattleHudLayout.PartyRowHeight;
            bool alive = actor.Actor.HitPoints > 0;
            UiColor nameColor = alive ? new(238, 238, 238) : new(148, 148, 164);
            UiColor hpColor = !alive ? new(255, 41, 24) : actor.Actor.HitPoints * 4L <= actor.Actor.MaxHitPoints ? new(255, 255, 123) : new(255, 255, 255);
            if (battle.ReadyPlayer == slot)
            {
                int phase = (int)(battle.Tick % 16);
                int shade = ((phase < 8 ? phase : 15 - phase) + 2) << 4;
                nameColor = BattleHudLayout.Modulate(nameColor, shade);
                hpColor = BattleHudLayout.Modulate(hpColor, shade);
            }
            int? mp = battle.GuestVitalsBindings.Any(binding => binding.HostSlot == slot) ? null : actor.Actor.MagicPoints;
            layout.DrawVitals(canvas, battle.Name(slot), actor.Actor.HitPoints, actor.Actor.MaxHitPoints, y, textScale, nameColor, hpColor, mp, actor.Actor.MaxMagicPoints);
            BattleHudElement.DrawGauge(canvas, layout.AtbGauge(y), actor.Gauge / (float)ushort.MaxValue, actor.AwaitingTurn, alive, false, 0);
            if (actor.LimitGauge != 0)
            {
                float fill = 100 * actor.LimitGauge / (float)byte.MaxValue;
                canvas.Fill(layout.Rect(832, y + 26, fill, 3), actor.LimitGauge == byte.MaxValue ? new(255, 255, 0) : new(240, 128, 128));
            }
            row++;
        }
    }

    private static void DrawBattleChoices(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, string caption, IReadOnlyList<BattleChoiceRow> choices, int selected, bool magic = false)
    {
        if (choices.Count == 0)
        {
            return;
        }
        selected = Math.Min(selected, choices.Count - 1);
        float scale = layout.Scale;
        float captionSize = UiTheme.DefaultCaptionFontSize * scale * textScale;
        float captionHeight = theme.CaptionContentHeight(captionSize) / scale;
        float fontSize = 20 * textScale;
        float nameWidth = choices.Max(choice => canvas.MeasureText(choice.Name, fontSize * scale, 4096 * scale).Width / scale);
        float valueWidth = choices.Max(choice => canvas.MeasureText(choice.Value, fontSize * scale, 4096 * scale).Width / scale);
        float valueGap = valueWidth > 0 ? 12 : 0;
        float naturalWidth = Math.Max(194, 38 + nameWidth + valueGap + valueWidth);
        BattleDrawLayout grid = magic ? BattleDrawLayout.Calculate(choices.Count) : default;
        float columnWidth = Math.Min(naturalWidth, magic ? 884f / grid.Columns : 440);
        float fit = Math.Min(1, (columnWidth - 38 - valueGap) / Math.Max(1, nameWidth + valueWidth));
        float rowHeight = magic ? Math.Min(28 * Math.Max(1, textScale), 584f / grid.Rows) : BattleHudLayout.CommandRowHeight;
        fontSize = Math.Min(fontSize * fit, rowHeight - 4);
        valueWidth *= fit;

        float contentOffset = Math.Max(14, magic ? captionHeight : Math.Max(captionHeight, captionSize * 0.75f / scale));
        UiRect panel;
        if (magic)
        {
            float height = contentOffset + grid.Rows * rowHeight + 6;
            panel = new(BattleHudLayout.CommandX, 648 - height, columnWidth * grid.Columns, height);
        }
        else
        {
            panel = BattleHudLayout.CommandPanel(columnWidth, choices.Count, contentOffset);
        }
        theme.Panel(canvas, layout.Rect(panel.X, panel.Y, panel.Width, panel.Height), caption, captionSize);
        int start = magic ? 0 : BattleHudLayout.CommandStart(selected, choices.Count);
        int end = magic ? choices.Count : Math.Min(choices.Count, start + BattleHudLayout.VisibleCommandRows);
        for (int index = start; index < end; index++)
        {
            (int column, int row) = magic ? grid.GetCell(index) : (0, index - start);
            float x = panel.X + column * columnWidth;
            float y = panel.Y + contentOffset + row * rowHeight;
            if (index == selected)
            {
                layout.DrawCommandPointer(canvas, theme, x, y);
            }
            UiRect name = layout.Rect(x + 30, y, columnWidth - 38 - valueWidth - valueGap, rowHeight);
            canvas.PushClip(name);
            try
            {
                canvas.Text(choices[index].Name, fontSize * scale, name, theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
            }
            finally
            {
                canvas.PopClip();
            }
            if (choices[index].Value.Length > 0)
            {
                canvas.AlignedText(choices[index].Value, fontSize * scale, layout.Rect(x + columnWidth - valueWidth - 8, y, valueWidth, rowHeight), theme.Foreground, UiAlignment.End);
            }
        }
    }

    private void DrawBattleTargets(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleSession battle, int ready, Ff7BattleCommandOption action)
    {
        Ff7BattleActorState[] targets = BattleTargets(battle, action);
        if (targets.Length == 0)
        {
            return;
        }
        int selected = Math.Min(battleTargetIndex, targets.Length - 1);
        bool enemies = targets[selected].Actor.Slot >= 4;
        float scale = layout.Scale;
        float fontSize = 24 * scale * textScale;
        float captionSize = UiTheme.DefaultCaptionFontSize * scale * textScale;
        int columns = (targets.Length + 3) / 4;
        (UiRect panel, float columnWidth, float rowHeight, float contentOffset) = layout.MeasureTargets(canvas, theme, targets.Select(target => battle.Name(target.Actor.Slot)), columns, textScale);
        string caption = enemies ? "TARGET  < ENEMIES >" : "TARGET  < ALLIES >";
        theme.Panel(canvas, layout.Rect(panel.X, panel.Y, panel.Width, panel.Height), caption, captionSize);
        for (int index = 0; index < targets.Length; index++)
        {
            int slot = targets[index].Actor.Slot;
            float x = panel.X + index / 4 * columnWidth;
            float rowY = panel.Y + contentOffset + index % 4 * rowHeight;
            float inset = columns > 1 ? 54 : 30;
            if (index == selected || (battleTargetGroup && action.GroupUses > 0 || !action.SelectSingle) && (slot >= 4) == enemies)
            {
                theme.Pointer(canvas, layout.Left + (x + (columns > 1 ? 3 : -48)) * scale, layout.Top + (rowY + 5) * scale, 24 * scale);
            }
            UiRect label = layout.Rect(x + inset, rowY, columnWidth - inset - 12, rowHeight);
            canvas.Text(battle.Name(slot), fontSize, label, theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
        }
        string title = CommandName(action, ready);
        if (action.GroupUses > 0)
        {
            title += battleTargetGroup ? "   All targets" : "   Single target";
        }
        float titleSize = 20 * scale * textScale;
        float titleHeight = canvas.MeasureText("Mg", titleSize, float.PositiveInfinity).Height / scale;
        float titleY = panel.Y - Math.Max(18, UiTheme.DefaultCaptionFontSize * textScale * 0.75f + 8) - titleHeight - 14;
        theme.Panel(canvas, layout.Rect(54, titleY, 440, titleHeight + 14));
        canvas.Text(title, titleSize, layout.Rect(68, titleY + 7, 412, titleHeight), theme.Foreground);
    }

    private static void DrawBattleResult(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleSession battle)
    {
        canvas.Fill(layout.Bounds, new(0, 0, 0));
        float fontSize = 20 * layout.Scale * textScale;
        string result = battle.Outcome switch
        {
            Ff7BattleOutcome.Victory => "Victory!",
            Ff7BattleOutcome.Escaped => "Escaped",
            _ => "Game Over"
        };
        theme.Panel(canvas, layout.ResultRect(16, 8, 352, 26));
        canvas.Text(result, fontSize, layout.ResultRect(26, 17, 332, 12), theme.Foreground);
        if (battle.Outcome == Ff7BattleOutcome.Victory)
        {
            Ff7BattleReward reward = battle.PreviewVictory();
            theme.Panel(canvas, layout.ResultRect(30, 34, 323, 50));
            canvas.Text($"{reward.Experience} EXP   {reward.AbilityPoints} AP   {reward.Gil} Gil", fontSize, layout.ResultRect(40, 41, 303, 12), theme.Foreground);
        }
        theme.Panel(canvas, layout.ResultRect(16, 184, 352, 26));
        canvas.AlignedText("Confirm", fontSize, layout.ResultRect(26, 192, 332, 12), theme.Foreground, UiAlignment.Center);
    }

    private void DrawBattleMessage(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleSession battle)
    {
        string? message = battle.EscapeBlocked ? "Can't escape!" : battle.Message ?? ActionTitle(battle);
        UiColor highlight = new(255, 255, 0);
        UiColor messageColor = theme.Foreground;
        if (mixedCombat?.ActiveGuest?.RenzokukenTrigger is { } trigger)
        {
            message ??= "Renzokuken";
            if (trigger.Widget is { Visible: true, ClosingPhase: 0, Input.Phase: not 8 } widget)
            {
                string binding = sharedInput.BindingHint(ControlAction.RotateRight);
                message = $"Renzokuken  {widget.Input.ContactIndex + 1}/{widget.Block.Timeline.InputThresholds.Length}  {binding} Trigger";
                messageColor = trigger.TriggerWindowOpen ? highlight : theme.Foreground;
            }
        }
        float fontSize = 20 * layout.Scale * textScale;
        if (message is { Length: > 0 })
        {
            theme.Panel(canvas, layout.MessagePanel);
            canvas.Text(message, fontSize, layout.MessageText, messageColor);
        }
    }

    private void DrawBattleDamage(IUiCanvas canvas, UiTheme theme, float scale, Rectangle sceneBounds, Ff7BattleSession battle)
    {
        if (battle.DisplayedHit is not { } hit || battle.Tick - hit.Tick >= Ff7BattleSession.LogicUpdatesPerSecond)
        {
            return;
        }
        System.Numerics.Vector3 anchor = mixedCombat?.GuestAt(hit.Target)?.Visual is { } guest
            ? CrossoverFf8BattleActorRenderer.EffectAnchor(guest)
            : battle.GuestEffectTargetAnchor(hit.Target);
        DrawBattleDamagePopup(canvas, theme.Foreground, scale, sceneBounds, battle.GuestRenderCameraOutput, anchor, hit.Damage, battle.Tick - hit.Tick);
    }

    internal static void DrawBattleDamagePopup(IUiCanvas canvas, UiColor foreground, float scale, Rectangle sceneBounds, SceneCameraPlacement camera, System.Numerics.Vector3 anchor, Ff7BattleDamageResult damage, long age)
    {
        if (Ff7BattleRenderer.ProjectWorldPoint(camera, sceneBounds.Width, sceneBounds.Height, anchor) is not { } position)
        {
            return;
        }
        string value = damage.Missed ? "Miss" : Math.Abs(damage.Damage).ToString(CultureInfo.InvariantCulture);
        float fontSize = 20 * scale;
        float rise = age * sceneBounds.Height / (12f * Ff7BattleSession.LogicUpdatesPerSecond);
        UiSize measured = canvas.MeasureText(value, fontSize, float.PositiveInfinity);
        UiRect text = new(sceneBounds.X + position.X - measured.Width / 2, sceneBounds.Y + position.Y - measured.Height - rise, measured.Width, measured.Height);
        canvas.OutlinedText(value, fontSize, text, damage.Damage < 0 ? new(255, 255, 0) : foreground, new(0, 0, 0), scale);
    }
}
