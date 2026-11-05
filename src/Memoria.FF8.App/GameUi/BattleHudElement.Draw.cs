using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private BattleDrawLayout drawLayout;
    private float drawColumnWidth;
    private float drawRowHeight;
    private UiRect drawPageArrow;
    private UiRect magicPreviousSectionArrow;
    private UiRect magicNextSectionArrow;

    private void DrawDrawList(IUiCanvas canvas, UiTheme theme)
    {
        DrawActionList(canvas, theme, false);
    }

    private void DrawMagicList(IUiCanvas canvas, UiTheme theme)
    {
        DrawActionList(canvas, theme, true);
    }

    private void DrawActionList(IUiCanvas canvas, UiTheme theme, bool magic)
    {
        IReadOnlyList<BattleActionDefinition> actions = controller.ActionChoices;
        IReadOnlyList<string> labels = controller.ChoiceLabels;
        drawLayout = BattleDrawLayout.Calculate(actions.Count);
        string caption = magic ? controller.MagicSectionCaption : controller.DrawCast ? "Cast" : "Draw";
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * controller.TextScale;
        float fontSize = 20 * controller.TextScale;
        float nameWidth = labels.Select(label => canvas.MeasureText(label, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
        float valueWidth = magic ? actions.Select(action => canvas.MeasureText(controller.ChoiceValue(action), fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max() : controller.DrawCast ? 0 : canvas.MeasureText("100", fontSize * Scale, 4096 * Scale).Width / Scale;
        float ownerWidth = magic && actions.Any(action => controller.MagicOwnerIcon(action).HasValue) ? 30 : 0;
        float naturalColumnWidth = Math.Max(194, 38 + nameWidth + ownerWidth + (valueWidth > 0 ? valueWidth + 12 : 0));
        if (magic)
        {
            float captionWidth = canvas.MeasureText(caption, captionSize, 4096 * Scale).Width / Scale + 24;
            naturalColumnWidth = Math.Max(naturalColumnWidth, captionWidth / drawLayout.Columns);
        }
        drawColumnWidth = Math.Min(naturalColumnWidth, 884 / (float)drawLayout.Columns);
        float fit = Math.Min(1, (drawColumnWidth - 38 - ownerWidth) / Math.Max(1, nameWidth + (valueWidth > 0 ? valueWidth + 12 : 0)));
        fontSize *= fit;
        valueWidth *= fit;
        drawRowHeight = Math.Min(28 * Math.Max(1, controller.TextScale), 584 / (float)drawLayout.Rows);
        fontSize = Math.Min(fontSize, drawRowHeight - 4);
        float contentOffset = Math.Max(14, theme.CaptionContentHeight(captionSize) / Scale);
        CommandWidth = drawColumnWidth * drawLayout.Columns;
        CommandPanelHeight = contentOffset + drawLayout.Rows * drawRowHeight + 6;
        CommandPanelY = 648 - CommandPanelHeight;
        CommandContentY = CommandPanelY + contentOffset;
        UiRect panel = Rect(30, CommandPanelY, CommandWidth, CommandPanelHeight);
        theme.Panel(canvas, panel, !magic && controller.DrawCast ? null : caption, captionSize);
        if (!magic && controller.DrawCast)
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
            bool unavailable = magic && (!actions[index].Available || controller.SelectedActor is { } caster && !controller.Session.QuoteMagic(caster.Id, actions[index].Id).Allowed);
            UiColor color = unavailable ? theme.Muted : !magic && controller.IsDrawGuardianForce(actions[index]) ? guardianColor : theme.Foreground;
            float textWidth = drawColumnWidth - 38 - ownerWidth - (valueWidth > 0 ? valueWidth + 12 : 0);
            canvas.PushClip(Rect(x + 30, y, textWidth, drawRowHeight));
            try
            {
                canvas.Text(labels[index], fontSize * Scale, Rect(x + 30, y, textWidth, drawRowHeight), color);
            }
            finally
            {
                canvas.PopClip();
            }
            if (magic && controller.MagicOwnerIcon(actions[index]) is { } portrait)
            {
                float portraitSize = Math.Min(26, drawRowHeight - 2);
                canvas.Image(portrait, Rect(x + 30 + textWidth, y + (drawRowHeight - portraitSize) / 2, portraitSize, portraitSize));
            }
            if (valueWidth > 0)
            {
                canvas.AlignedText(controller.ChoiceValue(actions[index]), fontSize * Scale, Rect(x + drawColumnWidth - valueWidth - 8, y, valueWidth, drawRowHeight), color, UiAlignment.End);
            }
        }
        if (magic)
        {
            magicPreviousSectionArrow = controller.MagicPreviousSectionAvailable ? Rect(6, CommandPanelY + CommandPanelHeight / 2 - 14, 24, 28) : default;
            magicNextSectionArrow = controller.MagicNextSectionAvailable ? Rect(30 + CommandWidth, CommandPanelY + CommandPanelHeight / 2 - 14, 24, 28) : default;
            DrawMagicSectionArrow(canvas, magicPreviousSectionArrow, true);
            DrawMagicSectionArrow(canvas, magicNextSectionArrow, false);
            return;
        }
        bool canSwitch = controller.DrawCast ? controller.DrawStockAvailable : controller.DrawCastAvailable;
        drawPageArrow = canSwitch ? Rect(controller.DrawCast ? 6 : 30 + CommandWidth, CommandPanelY + CommandPanelHeight / 2 - 14, 24, 28) : default;
        if (canSwitch)
        {
            int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate);
            canvas.Image(new UiImageSource(UiImageKind.Icon, 6, (phase & 2) != 0 ? 0 : 5, FlipHorizontal: controller.DrawCast), drawPageArrow);
        }
        if (controller.SelectedTarget is { } source)
        {
            DrawTargetModelPointers(canvas, theme, [source], false, source.Id);
        }
    }

    private void DrawMagicSectionArrow(IUiCanvas canvas, UiRect arrow, bool previous)
    {
        if (!arrow.IsEmpty)
        {
            int phase = (int)(controller.BattlePresentationSeconds * controller.BattleCallbackRate);
            canvas.Image(new UiImageSource(UiImageKind.Icon, 6, (phase & 2) != 0 ? 0 : 5, FlipHorizontal: previous), arrow);
        }
    }

    private void HoverMagicList(float x, float y)
    {
        int index = DrawChoiceAt(x, y);
        if (index >= 0)
        {
            controller.HoverMagicChoice(index);
        }
    }

    private void ClickMagicList(float x, float y)
    {
        if (controller.MagicPreviousSectionAvailable && !magicPreviousSectionArrow.IsEmpty && magicPreviousSectionArrow.Contains(x, y))
        {
            controller.ChangeMagicSection(-1);
            return;
        }
        if (controller.MagicNextSectionAvailable && !magicNextSectionArrow.IsEmpty && magicNextSectionArrow.Contains(x, y))
        {
            controller.ChangeMagicSection(1);
            return;
        }
        int index = DrawChoiceAt(x, y);
        if (index >= 0)
        {
            controller.Choose(index);
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
