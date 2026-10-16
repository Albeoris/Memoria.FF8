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
            canvas.Text(message, 20 * scale, content, theme.Foreground, UiAlignment.Center);
        }
    }
}
