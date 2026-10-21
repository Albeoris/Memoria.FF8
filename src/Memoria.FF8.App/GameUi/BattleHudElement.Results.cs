using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private UiRect ResultRect(float x, float y, float width, float height) => Rect(x * 2.5f, y * 3, width * 2.5f, height * 3);

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
        ResultLabel(canvas, theme, title, 26, 17, 332);
        theme.Panel(canvas, ResultRect(16, 184, 352, 26));
        ResultLabel(canvas, theme, ButtonPromptText.Mark(controller.NativeButtonBindingHint(0x40)) + "  " + controller.ResultText(22, "Confirm"), 26, 192, 332, true);
        if (controller.ResultPage == BattleResultPage.Experience)
        {
            DrawResultExperience(canvas, theme);
            if (rewards is null)
            {
                ResultLabel(canvas, theme, "Standalone battle: rewards are not saved.", 26, 213, 332, true, theme.Muted);
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
        else if (controller.ResultPage == BattleResultPage.AbilityPoints)
        {
            ResultPopup(canvas, theme, controller.ResultText(109, "Received {Var 32} AP!", controller.ResultAbilityPoints), 99);
        }
        else if (controller.ResultGuardianForce is { } growth)
        {
            string name = controller.ResultText(121, "G.F. ") + controller.GuardianForceDisplayName(growth.Id);
            if (growth.FinalLevel > growth.PreviousLevel)
            {
                ResultPopup(canvas, theme, name + " " + controller.ResultText(48, "Level Up!"), growth.LearnedAbilityIds.Length > 0 ? 45 : 99);
            }
            if (growth.LearnedAbilityIds.Length > 0)
            {
                theme.Panel(canvas, ResultRect(40, 88, 304, 56));
                ResultLabel(canvas, theme, name, 50, 95, 284);
                ResultLabel(canvas, theme, controller.ResultText(127, "Learned"), 62, 110, 272);
                int ability = growth.LearnedAbilityIds[0];
                if (NativeAbilityMenuMetadata.Icon(ability) is { } icon)
                {
                    canvas.Image(icon, ResultRect(74, 123, 12, 12));
                }
                ResultLabel(canvas, theme, controller.ResultLearnedAbility(ability) + controller.ResultText(126, " learned!"), 88, 125, 246);
            }
        }
    }

    private void DrawResultExperience(IUiCanvas canvas, UiTheme theme)
    {
        for (int index = 0; index < 3; index++)
        {
            float y = 34 + index * 50;
            theme.Panel(canvas, ResultRect(30, y, 323, 50));
            if (index >= controller.Party.Count)
            {
                continue;
            }
            BattleCombatant actor = controller.Party[index];
            (uint? total, int level, uint? next) = controller.DisplayedExperience(actor);
            ResultLabel(canvas, theme, actor.Name, 40, y + 7, 145, color: actor.IsAlive ? null : new UiColor(255, 96, 96));
            ResultLabel(canvas, theme, "Lv.", 40, y + 20, 20);
            ResultLabel(canvas, theme, level.ToString(CultureInfo.InvariantCulture), 92, y + 20, 30);
            ResultLabel(canvas, theme, controller.ResultText(29, "EXP received"), 196, y + 5, 74);
            ResultLabel(canvas, theme, controller.ResultText(30, "Current EXP"), 196, y + 20, 74);
            ResultLabel(canvas, theme, controller.ResultText(31, "Next level"), 196, y + 35, 74);
            string[] numbers = [controller.RemainingExperience(actor).ToString(CultureInfo.InvariantCulture), total?.ToString(CultureInfo.InvariantCulture) ?? "--", next?.ToString(CultureInfo.InvariantCulture) ?? "--"];
            for (int row = 0; row < numbers.Length; row++)
            {
                canvas.AlignedText(numbers[row], 20 * Scale * controller.TextScale, ResultRect(268, y + 7 + row * 15, 51, 12), theme.Foreground, UiAlignment.End);
                ResultLabel(canvas, theme, "EXP", 325, y + 7 + row * 15, 24);
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
}
