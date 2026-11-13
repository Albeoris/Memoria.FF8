using System.Globalization;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    internal readonly record struct BattleChoiceRow(string Name, string Value = "", bool Unsupported = false);
    internal readonly record struct BattleTargetRow(int Slot, string Name, string? UnsupportedReason = null, uint Statuses = 0);

    private void DrawBattle(Rectangle bounds, Rectangle sceneBounds, Ff7BattleSession battle)
    {
        IUiCanvas canvas = dialogueCanvas ?? throw new InvalidOperationException("FFVII battle requires the shared host presentation context.");
        UiTheme theme = SharedTheme();
        BattleHudLayout layout = new(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        UiPreferences preferences = PresentationPreferences;
        float textScale = preferences.TextScale / 100f * preferences.UiFontSize / 24f;
        battlePointerRows.Clear();
        if (battle.ResultReady)
        {
            DrawBattleResult(canvas, theme, layout, textScale, battle);
            return;
        }
        if (mixedCombat?.ActiveGuest?.PresentationUi is { } nativeUi && (nativeUi.Flags942 & 0x841) != 0x40)
        {
            return;
        }

        DrawBattleParty(canvas, theme, layout, textScale, battle);
        if (battle.BeatRushSelecting)
        {
            DrawBattleChoices(canvas, theme, layout, textScale, "BEAT RUSH", [new(battle.BeatRushVisible!.Value.ToString()), new("Confirm to stop")], 0);
        }
        else if (battle.ReadyPlayer is int ready)
        {
            Ff7BattleCommandOption[] commands = AvailableBattleCommands(battle, ready);
            if (selectedBattleCommand is { } selected && commands.FirstOrDefault(option => option.Command == selected.Command && option.Argument == selected.Argument) is { } current)
            {
                DrawBattleTargets(canvas, theme, layout, textScale, sceneBounds, battle, ready, current);
            }
            else if (battleCommandPage is { } page)
            {
                bool guest = IsMixedBattleSlot(ready);
                Ff7BattleCommandOption[] options = commands.Where(option => BattleCategory(option) == page).ToArray();
                BattleChoiceRow[] actions = options.Select(option => new BattleChoiceRow(CommandName(option, ready), BattleCommandValue(option, guest), option.UnsupportedReason is not null)).ToArray();
                int index = Array.FindIndex(options, option => option.Argument == highlightedBattleArgument);
                DrawBattleChoices(canvas, theme, layout, textScale, page.Name.ToUpperInvariant(), actions, Math.Max(0, index), page.Command == Ff7BattleCommand.Magic, (row, bounds) => battlePointerRows.Add((bounds, ready, page, options[row].Argument, null)));
            }
            else
            {
                var groups = commands.GroupBy(BattleCategory).ToArray();
                BattleChoiceRow[] categories = groups.Select(group => new BattleChoiceRow(group.Key.Command == Ff7BattleCommand.Limit && group.Count() == 1 ? CommandName(group.First(), ready) : group.Key.Name, Unsupported: group.All(option => option.UnsupportedReason is not null))).ToArray();
                int index = Array.FindIndex(groups, group => group.Key == highlightedBattleCategory);
                DrawBattleChoices(canvas, theme, layout, textScale, "COMMAND", categories, Math.Max(0, index), onRow: (row, bounds) => battlePointerRows.Add((bounds, ready, groups[row].Key, null, null)));
            }
        }
        DrawBattleMessage(canvas, theme, layout, textScale, battle);
        DrawBattleDamage(canvas, theme, layout.Scale * textScale, sceneBounds, battle);
        if (unavailableBattleCommand is { } unavailable)
        {
            DrawBattleUnsupportedAbility(canvas, theme, layout, textScale, unavailable);
        }
        DrawEnemyScan(canvas, theme, layout, textScale);
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

    private static void DrawBattleParty(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleSession battle)
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
            BattleHudElement.DrawGauge(canvas, layout.AtbGauge(y), actor.Gauge / (float)ushort.MaxValue, actor.AwaitingTurn, alive, false, Ff7AtbStatus(actor.Actor.Statuses));
            BattleHudElement.DrawStatusBadges(canvas, theme, layout, textScale, battle.Tick / (double)Ff7BattleSession.LogicUpdatesPerSecond, Ff7StatusBadges(actor.Actor.Statuses), 520, y + 25, 150);
            if (actor.LimitGauge != 0)
            {
                float fill = 100 * actor.LimitGauge / (float)byte.MaxValue;
                canvas.Fill(layout.Rect(832, y + 26, fill, 3), actor.LimitGauge == byte.MaxValue ? new(255, 255, 0) : new(240, 128, 128));
            }
            row++;
        }
    }

    internal static void DrawBattleChoices(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, string caption, IReadOnlyList<BattleChoiceRow> choices, int selected, bool magic = false, Action<int, UiRect>? onRow = null)
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
            onRow?.Invoke(index, layout.Rect(x, y, columnWidth, rowHeight));
            if (index == selected)
            {
                layout.DrawCommandPointer(canvas, theme, x, y);
            }
            UiColor color = choices[index].Unsupported ? new(255, 80, 80) : theme.Foreground;
            UiRect name = layout.Rect(x + 30, y, columnWidth - 38 - valueWidth - valueGap, rowHeight);
            canvas.PushClip(name);
            try
            {
                canvas.Text(choices[index].Name, fontSize * scale, name, color, UiAlignment.Start, UiTextFlow.SingleLine);
            }
            finally
            {
                canvas.PopClip();
            }
            if (choices[index].Value.Length > 0)
            {
                canvas.AlignedText(choices[index].Value, fontSize * scale, layout.Rect(x + columnWidth - valueWidth - 8, y, valueWidth, rowHeight), color, UiAlignment.End);
            }
        }
    }

    internal static void DrawBattleUnsupportedAbility(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleCommandOption option)
    {
        float scale = layout.Scale;
        float fontSize = 22 * scale * textScale;
        float width = 704 * scale;
        string reason = option.UnsupportedReason ?? throw new ArgumentException("An unavailable ability requires a reason.", nameof(option));
        float reasonHeight = canvas.MeasureText(reason, fontSize, width).Height;
        float hintHeight = canvas.MeasureText("Confirm or Cancel to return", fontSize, width).Height;
        float height = (reasonHeight + hintHeight) / scale + 80;
        float y = (672 - height) / 2;
        canvas.Fill(layout.Bounds, new(0, 0, 0, 160));
        theme.Panel(canvas, layout.Rect(112, y, 736, height), option.DisplayName ?? "Ability unavailable", UiTheme.DefaultCaptionFontSize * scale * textScale);
        canvas.Text(reason, fontSize, layout.Rect(128, y + 42, 704, reasonHeight / scale), new(255, 80, 80), UiAlignment.Start, UiTextFlow.Wrap);
        canvas.Text("Confirm or Cancel to return", fontSize, layout.Rect(128, y + height - hintHeight / scale - 14, 704, hintHeight / scale), theme.Foreground);
    }

    private void DrawBattleTargets(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Rectangle sceneBounds, Ff7BattleSession battle, int ready, Ff7BattleCommandOption action)
    {
        Ff7BattleActorState[] targets = BattleTargets(battle, action);
        if (targets.Length == 0)
        {
            return;
        }
        int selected = Math.Max(0, Array.FindIndex(targets, target => BattleTargetIdentity(target) == highlightedBattleTarget));
        BattleTargetRow[] rows = targets.Select(target => new BattleTargetRow(target.Actor.Slot, battle.Name(target.Actor.Slot), battle.GuestAbilityTargetUnsupportedReason(ready, action.Command, action.Argument, target.Actor.Slot), target.Actor.Statuses)).ToArray();
        bool group = battleTargetGroup && action.GroupUses > 0 || !action.SelectSingle;
        bool enemies = rows[selected].Slot >= 4;
        float viewportX = sceneBounds.Width / (float)triangles.Target.Width;
        float viewportY = sceneBounds.Height / (float)triangles.Target.Height;
        UiRect viewport = new(sceneBounds.X, sceneBounds.Y, sceneBounds.Width, sceneBounds.Height);
        for (int index = 0; index < rows.Length; index++)
        {
            int slot = rows[index].Slot;
            if ((index == selected || group && (action.AllTargets || (slot >= 4) == enemies)) && battleRenderer!.TryGetActorBounds(slot, out System.Numerics.Vector4 model))
            {
                UiRect projected = new(viewport.X + model.X * viewportX, viewport.Y + model.Y * viewportY, (model.Z - model.X) * viewportX, (model.W - model.Y) * viewportY);
                BattleHudElement.DrawModelPointer(canvas, theme, viewport, layout.Scale, projected);
            }
        }
        UiRect panel = DrawBattleTargetChoices(canvas, theme, layout, textScale, rows, selected, group, action.AllTargets, battle.Tick / (double)Ff7BattleSession.LogicUpdatesPerSecond, (row, bounds) => battlePointerRows.Add((bounds, ready, BattleCategory(action), action.Argument, BattleTargetIdentity(targets[row]))));
        float scale = layout.Scale;
        string title = CommandName(action, ready);
        bool multicast = action.Command == Ff7BattleCommand.Magic && battle.GuestMagicCastCounts(ready, action.Argument).Count > 1;
        if (multicast)
        {
            title += $" ×{battleMagicCastCount} ({battleMagicTargets.Count + 1}/{battleMagicCastCount})";
        }
        if (displayedGuestSmartPreview is { } smart)
        {
            title = smart.Name;
        }
        if (action.GroupUses > 0)
        {
            title += battleTargetGroup ? "   All targets" : "   Single target";
        }
        float titleSize = 20 * scale * textScale;
        float titleHeight = canvas.MeasureText("Mg", titleSize, float.PositiveInfinity).Height / scale;
        float titleY = panel.Y - Math.Max(18, UiTheme.DefaultCaptionFontSize * textScale * 0.75f + 8) - titleHeight - 14;
        float hintHeight = displayedGuestSmartPreview is null && !multicast ? 0 : titleHeight;
        titleY -= hintHeight;
        theme.Panel(canvas, layout.Rect(54, titleY, 440, titleHeight + hintHeight + 14));
        canvas.Text(title, titleSize, layout.Rect(68, titleY + 7, 412, titleHeight), theme.Foreground);
        if (displayedGuestSmartPreview is not null)
        {
            string hint = MagicUiText.Format("PhysicalOverride", sharedInput.BindingPrompt(ControlAction.CameraLeft));
            canvas.Text(hint, titleSize, layout.Rect(68, titleY + 7 + titleHeight, 412, titleHeight), theme.Foreground);
        }
        else if (multicast)
        {
            canvas.Text("Page Up/Down: number of casts", titleSize, layout.Rect(68, titleY + 7 + titleHeight, 412, titleHeight), theme.Foreground);
        }
    }

    internal static UiRect DrawBattleTargetChoices(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, IReadOnlyList<BattleTargetRow> targets, int selected, bool group, bool allTargets = false, double presentationSeconds = 0, Action<int, UiRect>? onRow = null)
    {
        selected = Math.Clamp(selected, 0, targets.Count - 1);
        bool enemies = targets[selected].Slot >= 4;
        float scale = layout.Scale;
        float fontSize = 24 * scale * textScale;
        float captionSize = UiTheme.DefaultCaptionFontSize * scale * textScale;
        int columns = (targets.Count + 3) / 4;
        (UiRect panel, float columnWidth, float rowHeight, float contentOffset) = layout.MeasureTargets(canvas, theme, targets.Select(target => target.Name), columns, textScale);
        string caption = group && allTargets ? "TARGET  < ALL >" : enemies ? "TARGET  < ENEMIES >" : "TARGET  < ALLIES >";
        theme.Panel(canvas, layout.Rect(panel.X, panel.Y, panel.Width, panel.Height), caption, captionSize);
        for (int index = 0; index < targets.Count; index++)
        {
            BattleTargetRow target = targets[index];
            float x = panel.X + index / 4 * columnWidth;
            float rowY = panel.Y + contentOffset + index % 4 * rowHeight;
            float inset = columns > 1 ? 54 : 30;
            onRow?.Invoke(index, layout.Rect(x, rowY, columnWidth, rowHeight));
            if (index == selected || group && (allTargets || (target.Slot >= 4) == enemies))
            {
                theme.Pointer(canvas, layout.Left + (x + (columns > 1 ? 3 : -48)) * scale, layout.Top + (rowY + 5) * scale, 24 * scale);
            }
            UiRect label = layout.Rect(x + inset, rowY, columnWidth - inset - 12, rowHeight);
            UiColor color = target.UnsupportedReason is null ? theme.Foreground : new(255, 80, 80);
            canvas.PushClip(label);
            try
            {
                canvas.Text(target.Name, fontSize, label, color, UiAlignment.Start, UiTextFlow.SingleLine);
            }
            finally
            {
                canvas.PopClip();
            }
            BattleHudElement.DrawStatusBadges(canvas, theme, layout, textScale, presentationSeconds, Ff7StatusBadges(target.Statuses), x + inset, rowY + 25, columnWidth - inset - 12);
        }
        return panel;
    }

    private BattleResultPager? battleResultPager;
    private string[] battleResultRows = [];
    private Ff7BattleReward? battleResultReward;

    private void PrepareBattleResult(Ff7Session session, Ff7BattleSession battle)
    {
        if (battleResultPager is not null)
        {
            return;
        }
        Ff7VictoryProjection result = session.PreviewBattleVictory();
        List<string> rows = [];
        foreach (Ff7VictoryCharacterGrowth growth in result.Characters)
        {
            uint experience = growth.Experience - growth.PreviousExperience;
            string level = growth.Level == growth.PreviousLevel ? "" : $"   Lv {growth.PreviousLevel} ? {growth.Level}";
            rows.Add($"{growth.Name}   +{experience} EXP{level}");
        }
        foreach (Ff7VictoryMateriaGrowth growth in result.Materia)
        {
            string learning = growth.Mastered ? "   Mastered" : growth.LearnedLevels > 0 ? $"   +{growth.LearnedLevels} rank" : "";
            string offspring = !growth.Mastered ? "" : growth.OffspringAdded ? "   +1 materia" : "   Materia inventory full";
            rows.Add($"{growth.CharacterName}   {ResultMateriaName(growth.MateriaId)}   +{unchecked(growth.EarnedAp - growth.PreviousAp) & 0xFFFFFF} AP{learning}{offspring}");
        }
        foreach (Ff7VictoryItem item in result.Items)
        {
            menuText ??= Ff7KernelTextResources.Load(installation);
            string name = item.ItemId >= 384 ? ResultMateriaName(item.ItemId - 384) : menuText.ItemNames.GetValueOrDefault(item.ItemId, $"Item {item.ItemId}");
            string rejected = item.Rejected > 0 ? $"   {item.Rejected} could not fit" : "";
            rows.Add($"{name}   ?{item.Accepted}{rejected}");
        }
        battleResultRows = rows.ToArray();
        battleResultReward = result.Reward;
        battleResultPager = new(battle.GuestVitalsBindings.Count == 0 ? 6 : 5);
    }

    private string ResultMateriaName(int id)
    {
        return id < 96 ? MateriaName(id) : $"Materia {id}";
    }

    private void DrawBattleResult(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleSession battle)
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
        if (battleResultReward is { } reward && battleResultPager is { } pager)
        {
            bool guests = battle.GuestVitalsBindings.Count != 0;
            DrawBattleRewards(canvas, theme, layout, textScale, reward, guests);
            string[] rows = pager.Rows(battleResultRows).ToArray();
            if (rows.Length > 0)
            {
                int start = guests ? 92 : 77;
                theme.Panel(canvas, layout.ResultRect(30, start, 323, rows.Length * 15 + 8));
                for (int index = 0; index < rows.Length; index++)
                {
                    UiRect row = layout.ResultRect(40, start + 4 + index * 15, 303, 14);
                    float measured = canvas.MeasureText(rows[index], fontSize, float.PositiveInfinity).Width;
                    canvas.Text(rows[index], fontSize * Math.Min(1, row.Width / Math.Max(1, measured)), row, theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
                }
            }
        }
        theme.Panel(canvas, layout.ResultRect(16, 184, 352, 26));
        canvas.AlignedText("Confirm", fontSize, layout.ResultRect(26, 192, 332, 12), theme.Foreground, UiAlignment.Center);
    }

    internal static void DrawBattleRewards(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, Ff7BattleReward reward, bool hasFf8Characters)
    {
        float fontSize = 20 * layout.Scale * textScale;
        theme.Panel(canvas, layout.ResultRect(30, 34, 323, hasFf8Characters ? 58 : 43));
        canvas.Text($"{reward.Experience} EXP   {reward.Gil} Gil", fontSize, layout.ResultRect(40, 41, 303, 12), theme.Foreground);
        canvas.Text($"{reward.AbilityPoints} AP", fontSize, layout.ResultRect(40, 56, 303, 12), theme.Foreground);
        if (hasFf8Characters)
        {
            canvas.Text($"{reward.Ff8AbilityPoints} AP (FF8)", fontSize, layout.ResultRect(40, 71, 303, 12), theme.Foreground);
        }
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
        DrawBattleDamagePopup(canvas, theme.Foreground, scale, sceneBounds, battle.GuestRenderCameraOutput, anchor, hit.Damage, battle.Tick - hit.Tick, hit.NativeResultMarker);
    }

    internal static void DrawBattleDamagePopup(IUiCanvas canvas, UiColor foreground, float scale, Rectangle sceneBounds, SceneCameraPlacement camera, System.Numerics.Vector3 anchor, Ff7BattleDamageResult damage, long age, int? nativeResultMarker = null)
    {
        if (Ff7BattleRenderer.ProjectWorldPoint(camera, sceneBounds.Width, sceneBounds.Height, anchor) is not { } position)
        {
            return;
        }
        string value = damage.Missed ? "Miss" : nativeResultMarker switch
        {
            -3 => "Full Cure",
            -2 => "Death",
            _ => Math.Abs(damage.Damage).ToString(CultureInfo.InvariantCulture)
        };
        float fontSize = 20 * scale;
        float rise = age * sceneBounds.Height / (12f * Ff7BattleSession.LogicUpdatesPerSecond);
        UiSize measured = canvas.MeasureText(value, fontSize, float.PositiveInfinity);
        UiRect text = new(sceneBounds.X + position.X - measured.Width / 2, sceneBounds.Y + position.Y - measured.Height - rise, measured.Width, measured.Height);
        canvas.OutlinedText(value, fontSize, text, damage.Damage < 0 ? new(255, 255, 0) : foreground, new(0, 0, 0), scale);
    }
}
