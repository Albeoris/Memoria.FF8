using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Completion names and parameters come from the same native control enums used by the encoder.</summary>
public static class LocalizationWorkbenchSyntax
{
    public static IReadOnlyList<string> Complete(string prefix, bool systemTemplate = false, string nativeLanguage = "en")
    {
        int open = prefix.LastIndexOf('{');
        string fragment = open < 0 ? "" : prefix[(open + 1)..];
        IEnumerable<string> values = systemTemplate ? Enumerable.Range(0, 10).Select(value => "{" + value + "}") : Enum.GetValues<TextTagCode>().Where(code => code != TextTagCode.Term).SelectMany(Controls).Concat(DictionaryControls(nativeLanguage));
        return values.Where(value => value.AsSpan(1).StartsWith(fragment, StringComparison.OrdinalIgnoreCase)).Take(32).ToArray();
    }
    private static IEnumerable<string> DictionaryControls(string nativeLanguage)
    {
        string suffix = nativeLanguage.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "JP" : nativeLanguage.ToUpperInvariant();
        Type? type = typeof(TextTagCode).Assembly.GetType(typeof(TextTagCode).Namespace + ".TextTagTerm" + suffix);
        return type is { IsEnum: true } ? Enum.GetNames(type).Select(name => "{Term" + suffix + " " + name + "}") : [];
    }
    private static IEnumerable<string> Controls(TextTagCode code)
    {
        string[]? names = code switch
        {
            TextTagCode.Char => Enum.GetNames<TextTagCharacter>(),
            TextTagCode.Color => Enum.GetNames<TextTagColor>(),
            TextTagCode.Key => Enum.GetNames<TextTagKey>(),
            TextTagCode.Dialog => Enum.GetNames<TextTagDialog>(),
            TextTagCode.Name => Enum.GetNames<TextTagName>(),
            TextTagCode.Var or TextTagCode.Item or TextTagCode.Pause or TextTagCode.Option or TextTagCode.Speed => ["0", "1", "2", "30"],
            TextTagCode.Glyph => ["204", "205", "226"],
            _ => null
        };
        return names is null ? ["{" + code + "}"] : names.Select(name => "{" + code + " " + name + "}");
    }
    public static IReadOnlyList<UiTextSpan> Highlight(string text, bool systemTemplate = false)
    {
        List<UiTextSpan> spans = [];
        foreach (Match match in Regex.Matches(text, "\\{[^{}]*\\}?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)))
        {
            string value = match.Value;
            int separator = value.IndexOfAny([' ', '}']);
            string name = separator > 0 ? value[1..separator] : value[1..];
            bool known = systemTemplate ? int.TryParse(name.Split(':')[0].Split(',')[0], out _) : Enum.TryParse(name, out TextTagCode _) || name.StartsWith("Term", StringComparison.Ordinal) || name.StartsWith('X') || name.StartsWith('W') || name.StartsWith('F');
            if (!known && !systemTemplate)
            {
                continue;
            }
            if (systemTemplate && (match.Index > 0 && text[match.Index - 1] == '{' || value.StartsWith("{{", StringComparison.Ordinal)))
            {
                continue;
            }
            spans.Add(new UiTextSpan(match.Index, match.Length, value.EndsWith('}') && known ? new UiColor(151, 202, 255) : new UiColor(255, 133, 133)));
        }
        return spans;
    }
}
