using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class VectorCrest : UiElement
{
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float x = Bounds.X + Bounds.Width * 0.42f;
        float y = Bounds.Y + Bounds.Height * 0.5f;
        UiColor color = new(174, 202, 230, 150);
        for (int index = 0; index < 3; index++)
        {
            float width = 18 - index * 5;
            float height = 32 - index * 7;
            canvas.Line(x, y - height, x + width, y, color, 0.7f);
            canvas.Line(x + width, y, x, y + height, color, 0.7f);
            canvas.Line(x, y + height, x - width, y, color, 0.7f);
            canvas.Line(x - width, y, x, y - height, color, 0.7f);
        }
        canvas.Line(Bounds.Right - 1, Bounds.Y, Bounds.Right - 1, Bounds.Bottom, color, 0.7f);
        canvas.Fill(new UiRect(Bounds.Right - 2, y - 1, 2, 2), color);
    }
}

internal sealed class VectorActiveTab : UiElement
{
    public VectorActiveTab(UiElement tab)
    {
        Children.Add(tab);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        theme.Selection(canvas, Bounds, underline: true);
    }
}

internal sealed class VectorText(string text, float fontSize, bool muted = false, UiAlignment horizontalAlignment = UiAlignment.Start) : UiElement
{
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        UiRect textBounds = Bounds;
        if (horizontalAlignment == UiAlignment.End)
        {
            float width = Math.Min(Bounds.Width, canvas.MeasureText(text, fontSize, float.PositiveInfinity).Width);
            textBounds = new UiRect(Bounds.Right - width, Bounds.Y, width, Bounds.Height);
        }
        canvas.Text(text, fontSize, textBounds, muted ? new UiColor(174, 202, 230) : theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
    }
}

internal sealed class VectorRuleContainer : UiElement
{
    private readonly bool bottom;

    public VectorRuleContainer(UiElement content, bool bottom = false)
    {
        this.bottom = bottom;
        Children.Add(content);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float y = bottom ? Bounds.Bottom - 1 : Bounds.Y;
        canvas.Line(Bounds.X, y, Bounds.Right, y, new UiColor(180, 197, 215, 140), 0.7f);
        canvas.Fill(new UiRect(Bounds.X - 1, y - 1, 2, 2), new UiColor(227, 235, 242));
    }
}

internal sealed class VectorReadouts((string Label, string Value)[] rows, float fontSize = 17) : UiElement
{
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (rows.Length == 0)
        {
            return;
        }
        float rowHeight = Math.Min(Bounds.Height / rows.Length, fontSize * 2.2f);
        for (int index = 0; index < rows.Length; index++)
        {
            var row = new UiRect(Bounds.X, Bounds.Y + index * rowHeight, Bounds.Width, rowHeight);
            canvas.Text(rows[index].Label, rows[index].Value.Length == 0 ? fontSize : fontSize * 0.72f, row, rows[index].Value.Length == 0 ? theme.Foreground : new UiColor(174, 202, 230), UiAlignment.Center, UiTextFlow.SingleLine);
            theme.RowSeparator(canvas, row);
            canvas.AlignedText(rows[index].Value, fontSize, new UiRect(row.X, row.Y + Math.Max(0, (row.Height - fontSize * 1.2f) / 2), row.Width, row.Height), theme.Foreground, UiAlignment.End);
        }
    }
}

internal sealed class VectorAttributeRow(string stat, string magic, string value, bool selected) : UiElement
{
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (selected)
        {
            theme.Selection(canvas, Bounds);
        }
        UiColor color = Enabled ? theme.Foreground : theme.Muted;
        float inset = 14;
        float size = Bounds.Width < 340 ? 14 : 20;
        canvas.Text(stat, size, new UiRect(Bounds.X + inset, Bounds.Y, Bounds.Width * 0.25f - inset, Bounds.Height), color, UiAlignment.Center, UiTextFlow.SingleLine);
        canvas.Text(magic, size, new UiRect(Bounds.X + Bounds.Width * 0.32f, Bounds.Y, Bounds.Width * 0.5f, Bounds.Height), color, UiAlignment.Center, UiTextFlow.SingleLine);
        canvas.AlignedText(value, size, new UiRect(Bounds.X, Bounds.Y + Math.Max(0, (Bounds.Height - 24) / 2), Bounds.Width - 8, Bounds.Height), color, UiAlignment.End);
        theme.RowSeparator(canvas, Bounds);
    }
}
