using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Identifies the standalone name line before a native field dialogue utterance.</summary>
public static partial class FieldSpeakerLabels
{
    public sealed record Label(int Start, int Length, string Text, int DialogueStart, bool HasMarker, bool StartsQuoted, bool IsStandaloneThought);
    public sealed record Page(int Start, int Length, Label? FirstLine);
    public sealed record Character(string Name, string Tag);

    private static readonly IReadOnlyDictionary<string, Character> KnownNames = BuildKnownNames();

    public static IReadOnlyList<Page> Read(string text)
    {
        FieldMessageContext.TryParseTag(0, text, out _, out int start);
        List<Page> pages = [];
        foreach (Match delimiter in PageBreak().Matches(text, start))
        {
            pages.Add(new(start, delimiter.Index - start, ReadLabel(text, start, delimiter.Index)));
            start = delimiter.Index + delimiter.Length;
        }
        pages.Add(new(start, text.Length - start, ReadLabel(text, start, text.Length)));
        return pages;
    }

    public static Character? LiteralCharacter(string name)
    {
        return KnownNames.GetValueOrDefault(name);
    }

    public static Character? SourceCharacter(string name)
    {
        // The original English field script also spells Selphie's name this way.
        return name == "Sephie" ? KnownNames["Selphie"] : LiteralCharacter(name);
    }

    public static Character? TaggedCharacter(string text)
    {
        if (!text.EndsWith('}') || text.Contains(','))
        {
            return null;
        }
        if (text.StartsWith("{Char ", StringComparison.Ordinal) && Enum.TryParse(text[6..^1], out TextTagCharacter character))
        {
            string? name = character switch
            {
                >= TextTagCharacter.Squall and <= TextTagCharacter.Ward or TextTagCharacter.Griever or TextTagCharacter.Mog or TextTagCharacter.Chocobo => character.ToString(),
                TextTagCharacter.Angelo1 or TextTagCharacter.Angelo2 => GameNames.DefaultName(2),
                TextTagCharacter.UnknownCharacter3 => GameNames.DefaultName(GameNames.GrieverSubject),
                _ => null
            };
            return name is null ? null : new(name, $"{{Char {character}}}");
        }
        if (text.StartsWith("{Name ", StringComparison.Ordinal) && Enum.TryParse(text[6..^1], out TextTagName named) && Enum.IsDefined(named))
        {
            return new(DisplayName(named), $"{{Name {named}}}");
        }
        return null;
    }

    private static IReadOnlyDictionary<string, Character> BuildKnownNames()
    {
        Dictionary<string, Character> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (TextTagCharacter value in Enum.GetValues<TextTagCharacter>())
        {
            if (value is >= TextTagCharacter.Squall and <= TextTagCharacter.Ward or TextTagCharacter.Mog or TextTagCharacter.Chocobo)
            {
                string name = value.ToString();
                names.Add(name, new(name, $"{{Char {value}}}"));
            }
        }
        foreach (TextTagName value in Enum.GetValues<TextTagName>())
        {
            if (value is TextTagName.Squall or TextTagName.Rinoa)
            {
                continue;
            }
            string name = DisplayName(value);
            Character identity = new(name, $"{{Name {value}}}");
            names.Add(name, identity);
            names.TryAdd(value.ToString(), identity);
        }
        return names;
    }

    private static string DisplayName(TextTagName name)
    {
        return name is >= TextTagName.Quetzacotl and <= TextTagName.Eden
            ? GameNames.DefaultName(GameNames.GuardianForceSubject((int)name - (int)TextTagName.Quetzacotl))
            : name.ToString();
    }

    public static bool IsOpeningQuote(char character)
    {
        return character is '"' or '“' or '”' or '«' or '„' or '‟';
    }

    internal static string ApplyTranslatedCharacterLabels(string source, string target)
    {
        IReadOnlyList<Page> sourcePages = Read(source);
        IReadOnlyList<Page> targetPages = Read(target);
        if (sourcePages.Count != targetPages.Count)
        {
            return source;
        }
        for (int index = sourcePages.Count - 1; index >= 0; index--)
        {
            Label? original = sourcePages[index].FirstLine;
            Label? translated = targetPages[index].FirstLine;
            if (original is null || translated is null || !CanApplySpeakerContract(original, translated))
            {
                continue;
            }
            string name = original.Text.TrimEnd(':');
            Character? expected = SourceCharacter(name);
            if (expected is not null && TaggedCharacter(translated.Text.TrimEnd(':'))?.Tag == expected.Tag)
            {
                source = string.Concat(source.AsSpan(0, original.Start), expected.Tag, source.AsSpan(original.Start + name.Length));
            }
        }
        return source;
    }

    public static bool CanApplySpeakerContract(Label source, Label target)
    {
        return source.HasMarker || source.StartsQuoted
            || source.IsStandaloneThought && target.IsStandaloneThought
                && (SourceCharacter(source.Text.TrimEnd(':')) ?? TaggedCharacter(source.Text.TrimEnd(':'))) is not null;
    }

    private static bool IsBalancedThought(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] is not ('(' or '（'))
        {
            return false;
        }
        int depth = 0;
        Span<char> expected = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '{')
            {
                int end = text[index..].IndexOf('}');
                if (end < 0)
                {
                    return false;
                }
                index += end;
            }
            else if (character is '(' or '（')
            {
                expected[depth++] = character == '(' ? ')' : '）';
            }
            else if (character is ')' or '）')
            {
                if (character != expected[--depth])
                {
                    return false;
                }
                if (depth == 0)
                {
                    return ThoughtSuffix().IsMatch(text[(index + 1)..]);
                }
            }
        }
        return false;
    }

    private static Label? ReadLabel(string text, int start, int end)
    {
        Match lineBreak = LineBreak().Match(text, start);
        if (!lineBreak.Success || lineBreak.Index + lineBreak.Length >= end)
        {
            return null;
        }

        string firstLine = text[start..lineBreak.Index];
        Match prefix = Prefix().Match(firstLine);
        Match suffix = Suffix().Match(firstLine, prefix.Length);
        int length = suffix.Index - prefix.Length;
        int dialogueStart = lineBreak.Index + lineBreak.Length;
        if (length <= 0)
        {
            return null;
        }
        string name = firstLine.Substring(prefix.Length, length);
        if (TaggedCharacter(name.TrimEnd(':')) is null && !LiteralLabel().IsMatch(name))
        {
            return null;
        }

        Match dialoguePrefix = Prefix().Match(text[dialogueStart..end]);
        int visibleStart = dialogueStart + dialoguePrefix.Length;
        if (visibleStart >= end)
        {
            return null;
        }
        return new(start + prefix.Length, length, name, dialogueStart, firstLine.Contains("{Speaker}", StringComparison.Ordinal), IsOpeningQuote(text[visibleStart]), IsBalancedThought(text.AsSpan(visibleStart, end - visibleStart)));
    }

    [GeneratedRegex(@"\{(?:Next|Page)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PageBreak();

    [GeneratedRegex(@"\{Line\}|\r\n|\r|\n", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"\A(?:[ \t]|\{(?:Speaker|Color [^{}\r\n]+|Pause [0-9]+)\})*", RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();

    [GeneratedRegex(@"(?:[ \t]|\{(?:Speaker|Color [^{}\r\n]+|Pause [0-9]+)\})*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Suffix();

    [GeneratedRegex(@"\A(?:\s|\{(?:End|Line|Color [^{}\r\n]+|Pause [0-9]+)\})*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ThoughtSuffix();

    [GeneratedRegex(@"\A[\p{L}\p{N}][\p{L}\p{N} .'’\-]{0,47}:?\z", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralLabel();
}
