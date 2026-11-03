using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private BattleDrawLayout drawLayout;
    private float drawColumnWidth;
    private float drawRowHeight;
    private UiRect drawPageArrow;

    private void DrawDrawList(IUiCanvas canvas, UiTheme theme)
    {
        IReadOnlyList<BattleActionDefinition> actions = controller.ActionChoices;
        IReadOnlyList<string> labels = controller.ChoiceLabels;
        drawLayout = BattleDrawLayout.Calculate(actions.Count);
        float fontSize = 20 * controller.TextScale;
        float nameWidth = labels.Select(label => canvas.MeasureText(label, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
        float valueWidth = controller.DrawCast ? 0 : canvas.MeasureText("100", fontSize * Scale, 4096 * Scale).Width / Scale;
        float naturalColumnWidth = Math.Max(194, 38 + nameWidth + (valueWidth > 0 ? valueWidth + 12 : 0));
        drawColumnWidth = Math.Min(naturalColumnWidth, 884 / (float)drawLayout.Columns);
        float fit = Math.Min(1, (drawColumnWidth - 38) / Math.Max(1, nameWidth + (valueWidth > 0 ? valueWidth + 12 : 0)));
        fontSize *= fit;
        valueWidth *= fit;
        drawRowHeight = Math.Min(28 * Math.Max(1, controller.TextScale), 584 / (float)drawLayout.Rows);
        fontSize = Math.Min(fontSize, drawRowHeight - 4);
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        float contentOffset = Math.Max(14, theme.CaptionContentHeight(captionSize) / Scale);
        CommandWidth = drawColumnWidth * drawLayout.Columns;
        CommandPanelHeight = contentOffset + drawLayout.Rows * drawRowHeight + 6;
        CommandPanelY = 648 - CommandPanelHeight;
        CommandContentY = CommandPanelY + contentOffset;
        UiRect panel = Rect(30, CommandPanelY, CommandWidth, CommandPanelHeight);
        string caption = controller.DrawCast ? "Cast" : "Draw";
        theme.Panel(canvas, panel, controller.DrawCast ? null : caption, captionSize);
        if (controller.DrawCast)
        {
            theme.Caption(canvas, panel, caption, captionSize, new UiColor(255, 255, 0));
        }
        UiColor guardianColor = LearnedAbilityHighlight.Color(controller.BattlePresentationSeconds);
        for (int index = 0; index < actions.Count; index++)
        {
            (int column, int row) = drawLayout.GetCell(index);
            float x = 30 + column * drawColumnWidth;
            float y = CommandContentY + row * drawRowHeight;
            if (index == controller.ChoiceIndex)
            {
                theme.Pointer(canvas, Left + (x + 8) * Scale, Top + (y + 5) * Scale, 10 * Scale);
            }
            float textWidth = drawColumnWidth - 38 - (valueWidth > 0 ? valueWidth + 12 : 0);
            canvas.PushClip(Rect(x + 30, y, textWidth, drawRowHeight));
            try
            {
                canvas.Text(labels[index], fontSize * Scale, Rect(x + 30, y, textWidth, drawRowHeight), controller.IsDrawGuardianForce(actions[index]) ? guardianColor : theme.Foreground);
            }
            finally
            {
                canvas.PopClip();
            }
            if (valueWidth > 0)
            {
                canvas.AlignedText(controller.ChoiceValue(actions[index]), fontSize * Scale, Rect(x + drawColumnWidth - valueWidth - 8, y, valueWidth, drawRowHeight), theme.Foreground, UiAlignment.End);
            }
        }
        bool canSwitch = controller.DrawCast ? controller.DrawStockAvailable : controller.DrawCastAvailable;
        drawPageArrow = canSwitch ? Rect(30 + CommandWidth, CommandPanelY + CommandPanelHeight / 2 - 14, 24, 28) : default;
        if (canSwitch)
        {
            int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate);
            canvas.Image(new UiImageSource(UiImageKind.Icon, 6, (phase & 2) != 0 ? 0 : 5), drawPageArrow);
        }
        if (controller.SelectedTarget is { } source)
        {
            DrawTargetModelPointers(canvas, theme, [source], false, source.Id);
        }
    }

    private int DrawChoiceAt(float x, float y)
    {
        if (!Rect(30, CommandContentY, CommandWidth, drawLayout.Rows * drawRowHeight).Contains(x, y))
        {
            return -1;
        }
        int column = (int)((x - Rect(30, 0, 0, 0).X) / (drawColumnWidth * Scale));
        int row = (int)((y - Rect(0, CommandContentY, 0, 0).Y) / (drawRowHeight * Scale));
        return drawLayout.GetIndex(column, row);
    }

    private void HoverDrawList(float x, float y)
    {
        int index = DrawChoiceAt(x, y);
        if (index >= 0)
        {
            controller.HoverDrawChoice(index);
        }
    }

    private void ClickDrawList(float x, float y)
    {
        if (!drawPageArrow.IsEmpty && drawPageArrow.Contains(x, y))
        {
            controller.ChangeDrawPage(!controller.DrawCast);
            return;
        }
        int index = DrawChoiceAt(x, y);
        if (index >= 0)
        {
            controller.Choose(index);
        }
    }
}
