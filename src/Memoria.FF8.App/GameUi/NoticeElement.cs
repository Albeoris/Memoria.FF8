using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Uses the battle message layout for compact, centered system notices.</summary>
internal sealed class NoticeElement(string message) : UiElement
{
    private readonly DialogueDocument document = DialogueTextFormatter.Parse(message);

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float scale = Math.Min(Bounds.Width / 960, Bounds.Height / 672);
        float left = Bounds.X + (Bounds.Width - 960 * scale) / 2;
        float top = Bounds.Y + (Bounds.Height - 672 * scale) / 2;
        UiRect panel = new(left + 240 * scale, top + 12 * scale, 480 * scale, 84 * scale);
        theme.Panel(canvas, panel);
        UiRect content = panel.Inset(14 * scale);
        if (canvas is IBattleMessageCanvas rich)
        {
            rich.BattleMessage(document, 20 * scale, content, 0);
        }
        else
        {
            float size = 20 * scale;
            List<string> lines = SystemTextLayout.Wrap(message, value => canvas.MeasureText(value, size, float.PositiveInfinity).Width, content.Width);
            float lineHeight = canvas.MeasureText("Mg", size, float.PositiveInfinity).Height;
            float y = content.Y + Math.Max(0, (content.Height - lines.Count * lineHeight) / 2);
            canvas.PushClip(content);
            try
            {
                foreach (string line in lines)
                {
                    canvas.AlignedText(line, size, new UiRect(content.X, y, content.Width, lineHeight), theme.Foreground, UiAlignment.Center);
                    y += lineHeight;
                }
            }
            finally
            {
                canvas.PopClip();
            }
        }
    }
}
