using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>One original text document, with selectable links on their authored lines.</summary>
internal sealed class NativeTutorialDocumentElement(string text, float fontSize, string fontFamily) : UiElement
{
    private readonly DialogueLine[] source = ReadSource(text);
    private readonly List<(int Line, UiButton Button)> links = [];
    private readonly Dictionary<string, float> advances = new(StringComparer.Ordinal);
    private DialogueVisualLine[] lines = [];
    private float renderedFontSize;
    private float lineHeight;
    private UiSize layoutSize;

    private static DialogueLine[] ReadSource(string text)
    {
        string icons = Regex.Replace(text, @"\{Key ([^{}]+)\}", match => Enum.TryParse(match.Groups[1].Value, out TextTagKey key) && NativeTutorialTextElement.Icon(key) is int icon ? ((char)(0xE000 + icon)).ToString() : match.Value);
        return DialogueTextFormatter.Parse(icons).Pages.SelectMany(page => page.Lines).ToArray();
    }

    private static bool IsIcon(char value)
    {
        return value is >= '\uE000' and < '\uE200';
    }

    private float Measure(string value, SystemTextMeasurer measurer)
    {
        float width = 0;
        int start = 0;
        for (int index = 0; index < value.Length; index++)
        {
            if (IsIcon(value[index]))
            {
                width += measurer.Measure(value[start..index]) + lineHeight;
                start = index + 1;
            }
        }
        return width + measurer.Measure(value[start..]);
    }

    internal void AddLink(int line, UiButton button)
    {
        links.Add((line, button));
        Children.Add(button);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        if (layoutSize != new UiSize(bounds.Width, bounds.Height))
        {
            layoutSize = new UiSize(bounds.Width, bounds.Height);
            LayoutDocument();
        }
        foreach ((int line, UiButton button) in links)
        {
            int first = Array.FindIndex(lines, visual => visual.SourceLineIndex == line);
            int count = lines.Count(visual => visual.SourceLineIndex == line);
            button.Visible = first >= 0;
            button.Arrange(new UiRect(bounds.X + 10, bounds.Y + Math.Max(0, first) * lineHeight, Math.Max(0, bounds.Width - 10), count * lineHeight));
        }
    }

    private void LayoutDocument()
    {
        float width = Math.Max(1, Bounds.Width - 35);
        int count = source.Length;
        while (count > 1 && string.IsNullOrWhiteSpace(source[count - 1].Text))
        {
            count--;
        }
        renderedFontSize = fontSize;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            using SystemTextMeasurer measure = new(renderedFontSize, fontFamily);
            lineHeight = Math.Max(measure.LineHeight, renderedFontSize * 1.4f);
            lines = source.Take(count).SelectMany(line => DialogueLayout.Wrap(line, width, value => Measure(value, measure), false, lineHeight)).ToArray();
            float height = lines.Length * lineHeight;
            if (height <= Bounds.Height || Bounds.Height <= 0)
            {
                break;
            }
            renderedFontSize *= Math.Min(0.95f, Bounds.Height / height);
        }
        advances.Clear();
        using SystemTextMeasurer finalMeasure = new(renderedFontSize, fontFamily);
        foreach (DialoguePlacedRun placed in lines.SelectMany(line => line.Runs))
        {
            foreach (string segment in Regex.Split(placed.Run.Text, "[\uE000-\uE1FF]"))
            {
                advances[segment] = finalMeasure.Measure(segment);
            }
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        for (int index = 0; index < lines.Length; index++)
        {
            foreach (DialoguePlacedRun placed in lines[index].Runs)
            {
                var color = placed.Run.Color;
                float x = Bounds.X + 35 + placed.X;
                string value = placed.Run.Text;
                if (placed.Run.IsButton)
                {
                    ButtonPrompt.DrawCanvas(canvas, value, renderedFontSize, new UiRect(x, Bounds.Y + index * lineHeight, Math.Max(0, Bounds.Right - x), lineHeight), 1, fontFamily);
                    continue;
                }
                int start = 0;
                for (int character = 0; character <= value.Length; character++)
                {
                    if (character == value.Length || IsIcon(value[character]))
                    {
                        string segment = value[start..character];
                        canvas.Text(segment, renderedFontSize, new UiRect(x, Bounds.Y + index * lineHeight, Math.Max(0, Bounds.Right - x), lineHeight), new UiColor(color.R, color.G, color.B, color.A), fontFamily);
                        x += advances[segment];
                        if (character < value.Length)
                        {
                            canvas.Image(new UiImageSource(UiImageKind.Icon, value[character] - 0xE000), new UiRect(x, Bounds.Y + index * lineHeight, lineHeight, lineHeight));
                            x += lineHeight;
                        }
                        start = character + 1;
                    }
                }
            }
        }
    }
}
