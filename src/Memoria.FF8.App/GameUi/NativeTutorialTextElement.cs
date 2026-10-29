using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

/// <summary>Places tutorial text and inline icons on the original page's sixteen-unit line grid.</summary>
internal sealed partial class NativeTutorialTextElement(string text, float nativeWidth, string? fontFamily) : UiElement
{
    public string Text { get; } = text;
    private readonly LocalizationTextBinding? binding = LocalizationTextTrace.Find(text);
    public float FontSize { get; init; } = 27;

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        Children.Clear();
        float scale = bounds.Width / nativeWidth;
        float lineHeight = 16 * scale;
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(FontSize, fontFamily);
        string[] lines = Text.Split('\n');
        for (int line = 0; line < lines.Length; line++)
        {
            float x = bounds.X;
            float y = bounds.Y + line * lineHeight;
            int cursor = 0;
            foreach (Match match in KeyTag().Matches(lines[line]))
            {
                if (!Enum.TryParse(match.Groups[1].Value, out TextTagKey key) || Icon(key) is not int icon)
                {
                    continue;
                }
                AddText(lines[line][cursor..match.Index]);
                UiImage image = new() { Source = new UiImageSource(UiImageKind.Icon, icon) };
                image.Arrange(new UiRect(x, y, lineHeight, lineHeight));
                Children.Add(image);
                x += lineHeight;
                cursor = match.Index + match.Length;
            }
            AddText(lines[line][cursor..]);

            void AddText(string value)
            {
                string visible = value.TrimStart(' ', '\t');
                int leading = value.Length - visible.Length;
                float left = x + measurer.Measure(value[..leading]);
                float advance = measurer.Measure(value);
                if (visible.Length > 0)
                {
                    string displayed = binding is null ? visible : LocalizationTextTrace.Attach(visible, binding);
                    TutorialTextRun run = new(displayed, FontSize, fontFamily);
                    run.Arrange(new UiRect(left, y, Math.Max(1, bounds.Right - left), lineHeight));
                    Children.Add(run);
                }
                x += advance;
            }
        }
    }

    internal static int? Icon(TextTagKey key)
    {
        // Original text tag 5 selects these ICON.SP1 entries through its key table.
        return key switch
        {
            TextTagKey.DefaultKey => 136,
            TextTagKey.JunctionMarker => 192,
            >= TextTagKey.JunctionAbility and <= TextTagKey.MenuAbility => 216 + (int)key - (int)TextTagKey.JunctionAbility,
            >= TextTagKey.FireElement and <= TextTagKey.HolyElement => 288 + (int)key - (int)TextTagKey.FireElement,
            >= TextTagKey.DeathStatus and <= TextTagKey.DrainStatus => 272 + (int)key - (int)TextTagKey.DeathStatus,
            _ => null
        };
    }

    [GeneratedRegex(@"\{Key ([^{}]+)\}")]
    private static partial Regex KeyTag();

    private sealed class TutorialTextRun(string value, float size, string? family) : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Text(value, size, Bounds, theme.Foreground, family);
        }
    }
}
