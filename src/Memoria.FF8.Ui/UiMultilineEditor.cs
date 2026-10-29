using System.Globalization;

namespace Memoria.FF8.Ui;

public enum UiEditCommand
{
    Left, Right, Up, Down, Home, End, Backspace, Delete, SelectAll, Copy, Cut, Paste, Undo, Redo, Confirm, Cancel, LineBreak, PageBreak, Complete, AcceptCompletion, PreviewPreviousPage, PreviewNextPage, PreviewPreviousChoice, PreviewNextChoice, TogglePicking
}

public sealed record UiEditorState(string Text, int Caret, int Anchor, (string Text, int Caret, int Anchor)[] Undo, (string Text, int Caret, int Anchor)[] Redo);

public readonly record struct UiTextSpan(int Start, int Length, UiColor Color);

/// <summary>Renderer-neutral Unicode editor. The host supplies committed text, IME composition and clipboard operations.</summary>
public sealed class UiMultilineEditor : UiElement
{
    private readonly Stack<(string Text, int Caret, int Anchor)> undo = new();
    private readonly Stack<(string Text, int Caret, int Anchor)> redo = new();
    private string text = "";
    private int anchor;
    private int firstLine;
    private float horizontalOffset;
    private IUiCanvas? measurementCanvas;
    public UiMultilineEditor()
    {
        Focusable = true;
    }
    public string Text => text;
    public int Caret { get; private set; }
    public int SelectionStart => Math.Min(anchor, Caret);
    public int SelectionLength => Math.Abs(anchor - Caret);
    public string SelectedText => text.Substring(SelectionStart, SelectionLength);
    public string Composition { get; private set; } = "";
    public bool IsComposing => Composition.Length != 0;
    public bool ReadOnly { get; set; }
    public float FontSize { get; set; } = 17;
    public IReadOnlyList<UiTextSpan> Syntax { get; set; } = [];
    public Func<string>? ReadClipboard { get; set; }
    public Action<string>? WriteClipboard { get; set; }
    public Action? BeforeEdit { get; set; }
    public Action? Changed { get; set; }
    public void SetText(string value, bool clearUndo = true)
    {
        text = value ?? "";
        Caret = Math.Min(Caret, text.Length);
        anchor = Math.Min(anchor, text.Length);
        Composition = "";
        if (clearUndo)
        {
            undo.Clear();
            redo.Clear();
        }
    }
    public UiEditorState CaptureState() => new(text, Caret, anchor, undo.ToArray(), redo.ToArray());
    public void RestoreState(UiEditorState state)
    {
        text = state.Text;
        Caret = state.Caret;
        anchor = state.Anchor;
        undo.Clear();
        redo.Clear();
        foreach (var entry in state.Undo.Reverse())
        {
            undo.Push(entry);
        }
        foreach (var entry in state.Redo.Reverse())
        {
            redo.Push(entry);
        }
        Composition = "";
    }
    public void Select(int start, int length)
    {
        anchor = Math.Clamp(start, 0, text.Length);
        Caret = Math.Clamp(start + length, 0, text.Length);
    }
    public void SetComposition(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            BeforeEdit?.Invoke();
        }
        Composition = value ?? "";
    }
    public void Insert(string value)
    {
        BeforeEdit?.Invoke();
        if (ReadOnly || string.IsNullOrEmpty(value))
        {
            return;
        }
        Remember();
        text = text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, value);
        Caret = SelectionStart + value.Length;
        anchor = Caret;
        Composition = "";
        Changed?.Invoke();
    }
    public bool Command(UiEditCommand command, bool shift = false, bool control = false)
    {
        if (IsComposing && command is UiEditCommand.Confirm or UiEditCommand.Cancel)
        {
            return true;
        }
        if (command == UiEditCommand.Copy)
        {
            WriteClipboard?.Invoke(SelectedText);
            return true;
        }
        if (command == UiEditCommand.SelectAll)
        {
            Select(0, text.Length);
            return true;
        }
        if (command is UiEditCommand.Paste or UiEditCommand.LineBreak)
        {
            Insert(command == UiEditCommand.Paste ? ReadClipboard?.Invoke() ?? "" : "\n");
            return true;
        }
        if (command is UiEditCommand.Cut or UiEditCommand.Delete or UiEditCommand.Backspace or UiEditCommand.Undo or UiEditCommand.Redo)
        {
            BeforeEdit?.Invoke();
            if (ReadOnly)
            {
                return true;
            }
            if (command is UiEditCommand.Undo or UiEditCommand.Redo)
            {
                var from = command == UiEditCommand.Undo ? undo : redo;
                var to = command == UiEditCommand.Undo ? redo : undo;
                if (from.TryPop(out var state))
                {
                    to.Push((text, Caret, anchor));
                    (text, Caret, anchor) = state;
                    Changed?.Invoke();
                }
                return true;
            }
            if (command == UiEditCommand.Cut)
            {
                WriteClipboard?.Invoke(SelectedText);
            }
            if (SelectionLength == 0 && command != UiEditCommand.Cut)
            {
                int other = command == UiEditCommand.Backspace ? Previous(Caret) : Next(Caret);
                anchor = other;
            }
            if (SelectionLength > 0)
            {
                Remember();
                int start = SelectionStart;
                text = text.Remove(start, SelectionLength);
                Caret = anchor = start;
                Changed?.Invoke();
            }
            return true;
        }
        int destination = Caret;
        switch (command)
        {
            case UiEditCommand.Left:
                destination = !shift && SelectionLength > 0 ? SelectionStart : Previous(Caret);
                break;
            case UiEditCommand.Right:
                destination = !shift && SelectionLength > 0 ? SelectionStart + SelectionLength : Next(Caret);
                break;
            case UiEditCommand.Home:
                destination = control ? 0 : LineStart(Caret);
                break;
            case UiEditCommand.End:
                destination = control ? text.Length : LineEnd(Caret);
                break;
            case UiEditCommand.Up:
            case UiEditCommand.Down:
                int column = Caret - LineStart(Caret);
                int adjacent = command == UiEditCommand.Up ? Math.Max(0, LineStart(Caret) - 1) : Math.Min(text.Length, LineEnd(Caret) + 1);
                destination = Math.Min(LineEnd(adjacent), LineStart(adjacent) + column);
                break;
            default:
                return false;
        }
        Caret = Boundary(destination);
        if (!shift)
        {
            anchor = Caret;
        }
        return true;
    }
    private int Boundary(int index) => index == text.Length ? index : StringInfo.ParseCombiningCharacters(text).LastOrDefault(value => value <= index);
    private int Previous(int index)
    {
        return StringInfo.ParseCombiningCharacters(text).LastOrDefault(value => value < index);
    }
    private int Next(int index)
    {
        return StringInfo.ParseCombiningCharacters(text).FirstOrDefault(value => value > index, text.Length);
    }
    private int LineStart(int index) => index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
    private int LineEnd(int index)
    {
        int end = text.IndexOf('\n', index);
        return end < 0 ? text.Length : end;
    }
    private void Remember()
    {
        undo.Push((text, Caret, anchor));
        redo.Clear();
    }
    public void ExtendPointerSelection(float x, float y)
    {
        int previousAnchor = anchor;
        Pointer(x, y);
        anchor = previousAnchor;
    }
    public override void Pointer(float x, float y)
    {
        int line = firstLine + Math.Max(0, (int)((y - Bounds.Y - 6) / (FontSize * 1.5f)));
        string[] lines = text.Split('\n');
        line = Math.Min(line, lines.Length - 1);
        int start = lines.Take(line).Sum(value => value.Length + 1);
        float distance = x - Bounds.X - 6 + horizontalOffset;
        int column = Math.Clamp((int)(distance / (FontSize * .6f)), 0, lines[line].Length);
        if (measurementCanvas is not null)
        {
            int[] stops = StringInfo.ParseCombiningCharacters(lines[line]).Append(lines[line].Length).ToArray();
            column = stops.MinBy(stop => Math.Abs(Measure(measurementCanvas, lines[line][..stop]) - distance));
        }
        Caret = anchor = Boundary(start + column);
    }
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        measurementCanvas = canvas;
        float caretX = Measure(canvas, text[LineStart(Caret)..Caret]);
        horizontalOffset = Math.Clamp(horizontalOffset, Math.Max(0, caretX - Bounds.Width + 18), Math.Max(0, caretX));
        canvas.Fill(Bounds, new UiColor(18, 20, 28));
        float height = FontSize * 1.5f;
        int caretLine = text.Take(Caret).Count(value => value == '\n');
        int visible = Math.Max(1, (int)((Bounds.Height - 12) / height));
        firstLine = Math.Clamp(firstLine, Math.Max(0, caretLine - visible + 1), caretLine);
        int start = 0;
        int lineIndex = 0;
        foreach (string line in text.Split('\n'))
        {
            if (lineIndex >= firstLine && lineIndex < firstLine + visible)
            {
                float y = Bounds.Y + 6 + (lineIndex - firstLine) * height;
                float X(int offset) => Bounds.X + 6 - horizontalOffset + Measure(canvas, line[..Math.Clamp(offset, 0, line.Length)]);
                int left = Math.Max(start, SelectionStart);
                int right = Math.Min(start + line.Length, SelectionStart + SelectionLength);
                if (right > left)
                {
                    canvas.Fill(new UiRect(X(left - start), y, X(right - start) - X(left - start), height), theme.Highlight);
                }
                canvas.Text(line, FontSize, new UiRect(Bounds.X + 6 - horizontalOffset, y, Math.Max(0, Bounds.Width - 12 + horizontalOffset), height), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
                foreach (UiTextSpan span in Syntax)
                {
                    int a = Math.Max(start, span.Start);
                    int b = Math.Min(start + line.Length, span.Start + span.Length);
                    if (b > a)
                    {
                        canvas.Text(text[a..b], FontSize, new UiRect(X(a - start), y, Math.Max(0, Bounds.Right - X(a - start)), height), span.Color, UiAlignment.Start, UiTextFlow.SingleLine);
                    }
                }
                if (Caret >= start && Caret <= start + line.Length)
                {
                    float x = X(Caret - start);
                    canvas.Line(x, y, x, y + height, theme.Foreground, 1);
                    if (IsComposing)
                    {
                        canvas.Text(Composition, FontSize, new UiRect(x, y, Math.Max(0, Bounds.Right - x), height), theme.SliderLight);
                        canvas.Line(x, y + height, x + Measure(canvas, Composition), y + height, theme.SliderLight, 1);
                    }
                }
            }
            start += line.Length + 1;
            lineIndex++;
        }
    }
    private float Measure(IUiCanvas canvas, string value)
    {
        try
        {
            return canvas.MeasureText(value, FontSize, float.PositiveInfinity).Width;
        }
        catch (NotSupportedException)
        {
            return value.Length * FontSize * .6f;
        }
    }
}
