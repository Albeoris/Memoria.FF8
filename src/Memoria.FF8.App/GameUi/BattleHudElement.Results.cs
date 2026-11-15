using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private UiRect ResultRect(float x, float y, float width, float height) => Layout.ResultRect(x, y, width, height);

    private void ResultLabel(IUiCanvas canvas, UiTheme theme, string text, float x, float y, float width, bool centered = false, UiColor? color = null)
    {
        canvas.AlignedText(text, 20 * Scale * controller.TextScale, ResultRect(x, y, width, 12), color ?? theme.Foreground, centered ? UiAlignment.Center : UiAlignment.Start);
    }

    private void ResultPopup(IUiCanvas canvas, UiTheme theme, string text, float y, float height = 26)
    {
        float fontSize = 20 * Scale * controller.TextScale;
        UiSize measured = canvas.MeasureText(text, fontSize, 332 * 2.5f * Scale);
        float width = Math.Clamp(measured.Width / (2.5f * Scale) + 20, 50, 352);
        float panelHeight = Math.Max(height, measured.Height / (3 * Scale) + 14);
        float x = (384 - width) / 2;
        float top = y + (height - panelHeight) / 2;
        theme.Panel(canvas, ResultRect(x, top, width, panelHeight));
        canvas.AlignedText(text, fontSize, ResultRect(x + 10, top + 7, width - 20, panelHeight - 14), theme.Foreground, UiAlignment.Center);
    }

    private void DrawResults(IUiCanvas canvas, UiTheme theme)
    {
        BattleRewardSummary? rewards = controller.ResultRewards;
        canvas.Fill(Bounds, new UiColor(0, 0, 0));
        theme.Panel(canvas, ResultRect(16, 8, 352, 26));
        string title = controller.ResultPage switch
        {
            BattleResultPage.Experience => controller.ResultText(23, "EXP received"),
            BattleResultPage.Items => controller.ResultText(21, "Items received"),
            _ => controller.ResultText(111, "G.F.")
        };
        if (controller.Session.Outcome == BattleOutcome.Escaped)
        {
            title = controller.FormatSystemText("battle.results.escaped", "Escaped - {0}", title);
        }
        ResultLabel(canvas, theme, title, 26, 17, 332);
        theme.Panel(canvas, ResultRect(16, 184, 352, 26));
        ResultLabel(canvas, theme, ButtonPromptText.Mark(controller.NativeButtonBindingHint(0x40)) + "  " + controller.ResultText(22, "Confirm"), 26, 192, 332, true);
        if (controller.ResultPage == BattleResultPage.Experience)
        {
            DrawResultExperience(canvas, theme);
            if (rewards is null)
            {
                ResultLabel(canvas, theme, controller.SystemText("battle.results.standalone", "Standalone battle: rewards are not saved."), 26, 213, 332, true, theme.Muted);
            }
        }
        else if (controller.ResultPage == BattleResultPage.Items)
        {
            int index = controller.ResultItemIndex;
            int count = controller.ResultLoot.Count;
            if (index >= count)
            {
                ResultPopup(canvas, theme, controller.ResultText(28, "No items found."), 113);
                return;
            }
            theme.Panel(canvas, ResultRect(72, 71, 240, 26));
            ResultLabel(canvas, theme, controller.ResultItemName(index), 82, 80, 196);
            int quantity = controller.ResultLoot[index].Quantity;
            canvas.AlignedText(quantity.ToString(CultureInfo.InvariantCulture), 20 * Scale * controller.TextScale, ResultRect(280, 80, 22, 12), theme.Foreground, UiAlignment.End);
            string description = controller.ResultDescription(index);
            if (!string.IsNullOrEmpty(description))
            {
                theme.Panel(canvas, ResultRect(16, 156, 352, 26));
                ResultLabel(canvas, theme, description, 26, 163, 332);
            }
            if (controller.ItemAcknowledged)
            {
                ResultPopup(canvas, theme, controller.ResultText(controller.ResultLoot[index].Accepted ? 6 : 24, controller.ResultLoot[index].Accepted ? "Received!" : "Cannot carry any more.", quantity, controller.ResultItemName(index)), 113);
            }
        }
        else if (controller.ResultPage == BattleResultPage.GuardianForce)
        {
            DrawResultGuardianForces(canvas, theme);
        }
    }

    private void DrawResultGuardianForces(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, ResultRect(16, 38, 352, 142));
        ResultLabel(canvas, theme, controller.ResultText(109, "Received {Var 32} AP!", controller.ResultAbilityPoints), 26, 44, 332);
        IReadOnlyList<(BattleGuardianForceRewardPresentation Growth, int? AbilityId)> entries = controller.ResultGuardianForceRows;
        float glow = (float)((Math.Cos(controller.ResultAnimationSeconds * Math.Tau) + 1) / 2);
        UiColor highlight = new(255, (byte)(208 + 47 * glow), (byte)(96 + 159 * glow));
        for (int index = 0; index < entries.Count; index++)
        {
            (BattleGuardianForceRewardPresentation growth, int? ability) = entries[index];
            float x = 26 + index / 8 * 170;
            float y = 61 + index % 8 * 14.5f;
            bool leveled = growth.FinalLevel > growth.PreviousLevel;
            canvas.Image(new UiImageSource(UiImageKind.GuardianForcePortrait, growth.Id), ResultRect(x, y, 13, 13));
            if (leveled)
            {
                ResultLabel(canvas, theme, "↑", x + 14, y + 1, 9, color: highlight);
            }
            ResultLabel(canvas, theme, controller.FormatSystemText("battle.results.level", "Lv. {0}", growth.FinalLevel), x + 24, y + 1, 38, color: leveled ? highlight : theme.Foreground);
            if (ability is int abilityId)
            {
                ResultLabel(canvas, theme, controller.ResultLearnedAbility(abilityId), x + 64, y + 1, 98, color: highlight);
            }
        }
    }

    private void DrawResultExperience(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleCombatant> actors = controller.Party;
        if (actors.Count > 4)
        {
            DrawExpandedResultExperience(canvas, theme, actors);
            return;
        }
        if (actors.Count == 4)
        {
            DrawFourMemberResultExperience(canvas, theme, actors);
            return;
        }
        for (int index = 0; index < 3; index++)
        {
            float y = 34 + index * 50;
            theme.Panel(canvas, ResultRect(30, y, 323, 50));
            if (index >= actors.Count)
            {
                continue;
            }
            BattleCombatant actor = actors[index];
            (uint? total, int level, uint? next) = controller.DisplayedExperience(actor);
            ResultLabel(canvas, theme, actor.Name, 40, y + 7, 145, color: actor.IsAlive ? null : new UiColor(255, 96, 96));
            ResultLabel(canvas, theme, controller.SystemText("battle.results.level-label", "Lv."), 40, y + 20, 20);
            ResultLabel(canvas, theme, level.ToString(CultureInfo.InvariantCulture), 92, y + 20, 30);
            ResultLabel(canvas, theme, controller.ResultText(29, "EXP received"), 196, y + 5, 74);
            ResultLabel(canvas, theme, controller.ResultText(30, "Current EXP"), 196, y + 20, 74);
            ResultLabel(canvas, theme, controller.ResultText(31, "Next level"), 196, y + 35, 74);
            string[] numbers = [controller.RemainingExperience(actor).ToString(CultureInfo.InvariantCulture), total?.ToString(CultureInfo.InvariantCulture) ?? "--", next?.ToString(CultureInfo.InvariantCulture) ?? "--"];
            for (int row = 0; row < numbers.Length; row++)
            {
                canvas.AlignedText(numbers[row], 20 * Scale * controller.TextScale, ResultRect(268, y + 7 + row * 15, 51, 12), theme.Foreground, UiAlignment.End);
                ResultLabel(canvas, theme, controller.SystemText("battle.results.experience-label", "EXP"), 325, y + 7 + row * 15, 24);
            }
            if (controller.ExperienceFinished && controller.CharacterResult(actor) is { } growth && growth.FinalLevel > growth.PreviousLevel)
            {
                ResultLabel(canvas, theme, controller.ResultText(48, "Level Up!"), 40, y + 35, 145);
            }
            else if (controller.AwardedExperience(actor) == 0)
            {
                ResultLabel(canvas, theme, controller.ResultText(49, "No EXP received."), 40, y + 35, 145);
            }
        }
    }

    private void DrawFourMemberResultExperience(IUiCanvas canvas, UiTheme theme, IReadOnlyList<BattleCombatant> actors)
    {
        const float x = 16;
        const float width = 352;
        const float height = 37;
        for (int index = 0; index < actors.Count; index++)
        {
            BattleCombatant actor = actors[index];
            float y = 34 + index * height;
            theme.Panel(canvas, ResultRect(x, y, width, height));
            (uint? total, int level, uint? next) = controller.DisplayedExperience(actor);
            UiColor? nameColor = actor.IsAlive ? null : new UiColor(255, 96, 96);
            ResultLabel(canvas, theme, actor.Name, x + 8, y + 3, width - 46, color: nameColor);
            string levelLabel = controller.SystemText("battle.results.level-label", "Lv.");
            ResultLabel(canvas, theme, $"{levelLabel} {level}", x + width - 36, y + 3, 32);
            DrawAmount(controller.ResultText(29, "EXP received"), controller.RemainingExperience(actor).ToString(CultureInfo.InvariantCulture), x + 8, y + 14);
            DrawAmount(controller.ResultText(30, "Current EXP"), total?.ToString(CultureInfo.InvariantCulture) ?? "--", x + 8, y + 24);
            DrawAmount(controller.ResultText(31, "Next level"), next?.ToString(CultureInfo.InvariantCulture) ?? "--", x + width / 2 + 5, y + 14);
            if (controller.ExperienceFinished && controller.CharacterResult(actor) is { } growth && growth.FinalLevel > growth.PreviousLevel)
            {
                ResultLabel(canvas, theme, controller.ResultText(48, "Level Up!"), x + width / 2 + 5, y + 24, width / 2 - 8);
            }
        }

        void DrawAmount(string label, string amount, float left, float top)
        {
            float fontSize = 15 * Scale * controller.TextScale;
            const float labelWidth = width / 2 - 30;
            canvas.Text(label, fontSize, ResultRect(left, top, labelWidth, 11), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
            canvas.AlignedText(amount, fontSize, ResultRect(left + labelWidth, top, 24, 11), theme.Foreground, UiAlignment.End);
        }
    }

    private void DrawExpandedResultExperience(IUiCanvas canvas, UiTheme theme, IReadOnlyList<BattleCombatant> actors)
    {
        int columns = actors.Count <= 8 ? 2 : 3;
        int rows = (actors.Count + columns - 1) / columns;
        const float gap = 3;
        float width = (352 - (columns - 1) * gap) / columns;
        float height = (150 - (rows - 1) * gap) / rows;
        string receivedLabel = controller.ResultText(29, "EXP received");
        string totalLabel = controller.ResultText(30, "Current EXP");
        string nextLabel = controller.ResultText(31, "Next level");
        for (int index = 0; index < actors.Count; index++)
        {
            BattleCombatant actor = actors[index];
            float x = 16 + index / rows * (width + gap);
            float y = 34 + index % rows * (height + gap);
            theme.Panel(canvas, ResultRect(x, y, width, height));
            UiRect content = ResultRect(x + 4, y + 2, width - 8, height - 4);
            (uint? total, int level, uint? next) = controller.DisplayedExperience(actor);
            bool leveled = controller.ExperienceFinished && controller.CharacterResult(actor) is { } growth && growth.FinalLevel > growth.PreviousLevel;
            string levelLabel = controller.FormatSystemText("battle.results.level", "Lv. {0}", level);
            if (leveled)
            {
                levelLabel = "↑ " + levelLabel;
            }
            (string Label, string Value)[] lines =
            [
                (actor.Name, levelLabel),
                (receivedLabel, controller.RemainingExperience(actor).ToString(CultureInfo.InvariantCulture)),
                (totalLabel, total?.ToString(CultureInfo.InvariantCulture) ?? "--"),
                (nextLabel, next?.ToString(CultureInfo.InvariantCulture) ?? "--")
            ];

            float lineHeight = content.Height / lines.Length;
            float textGap = 4 * Scale;
            float requestedSize = Math.Min(20 * Scale * controller.TextScale, lineHeight);
            float fit = 1;
            foreach ((string label, string value) in lines)
            {
                UiSize left = canvas.MeasureText(label, requestedSize, float.MaxValue);
                UiSize right = canvas.MeasureText(value, requestedSize, float.MaxValue);
                fit = Math.Min(fit, Math.Min((content.Width - textGap) / Math.Max(1, left.Width + right.Width), lineHeight / Math.Max(1, Math.Max(left.Height, right.Height))));
            }
            float fontSize = requestedSize * fit;
            for (int line = 0; line < lines.Length; line++)
            {
                (string label, string value) = lines[line];
                float valueWidth = canvas.MeasureText(value, fontSize, content.Width - textGap).Width;
                float top = content.Y + line * lineHeight;
                UiRect labelBounds = new(content.X, top, content.Width - valueWidth - textGap, lineHeight);
                UiRect valueBounds = new(content.X + content.Width - valueWidth, top, valueWidth, lineHeight);
                UiColor labelColor = line == 0 && !actor.IsAlive ? new UiColor(255, 96, 96) : theme.Foreground;
                UiColor valueColor = line == 0 && leveled ? new UiColor(255, 224, 128) : theme.Foreground;
                canvas.Text(label, fontSize, labelBounds, labelColor, UiAlignment.Center, UiTextFlow.SingleLine);
                canvas.Text(value, fontSize, valueBounds, valueColor, UiAlignment.Center, UiTextFlow.SingleLine);
            }
        }
    }
}
