using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal static partial class TranslationReviewRules
{
    private const string Information = "Information and SeeD exam";
    private const string End = "{End}";
    private const string UnsupportedKey = "tag-key-unsupported";
    private static readonly IReadOnlyDictionary<string, string> SystemSources = SystemTextCatalog.SourceEntries().ToDictionary(entry => entry.Key, entry => entry.Text, StringComparer.Ordinal);
    private static readonly Dictionary<string, string> NativeTagNames = Enum.GetNames<TextTagCode>()
        .Where(name => name != nameof(TextTagCode.Term))
        .Concat(["Page", "TermEN", "TermJP", "TermDE", "TermFR", "TermIT", "TermES", "Info", "Empty", "Invalid"])
        .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

    private readonly record struct Tag(int Start, int Length, string Raw, string Kind, string? Identity, string? Normalized, string? Issue)
    {
        public bool Dynamic => Issue is null && Identity is not null;
    }

    private static bool IsInvalidPresentation(string text, string category, string key)
    {
        if (!text.Contains(LocalizationValue.InvalidTarget, StringComparison.Ordinal))
        {
            return false;
        }
        int position = 0;
        foreach (Tag tag in ReadTags(text, category, key))
        {
            if (!text.AsSpan(position, tag.Start - position).IsWhiteSpace()
                || tag.Issue is null && tag.Kind is not ("Invalid" or "Field" or "Color" or "Line" or "Page" or "Next" or "End" or "Pause" or "Speed" or "Speaker" or "Option"))
            {
                return false;
            }
            position = tag.Start + tag.Length;
        }
        return text.AsSpan(position).IsWhiteSpace();
    }

    private static void ReviewTagSyntax(ReviewState state, string? reference, string category, string key)
    {
        if (state.Target.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            state.Report("tag-malformed", "Text contains a raw control character.");
        }
        if (TryCompositeSource(category, key, reference, out _))
        {
            return;
        }
        List<Edit> edits = [];
        Tag[] targetTags = ReadTags(state.Target, category, key).ToArray();
        Tag[]? referenceTags = null;
        foreach (Tag tag in targetTags)
        {
            if (tag.Issue is null)
            {
                int following = tag.Start + tag.Length;
                if (category == "Fields" && tag.Kind is "Char" or "Name" && FollowingNameAffix().IsMatch(state.Target, following))
                {
                    state.Report("tag-name-affix", $"Verify the Cyrillic suffix attached to dynamic name '{tag.Raw}'.");
                }
                continue;
            }
            if (tag.Issue == UnsupportedKey)
            {
                state.Report(UnsupportedKey, $"Control '{tag.Raw}' at character {tag.Start + 1} has no supported button binding or native sprite.");
                continue;
            }
            string? replacement = tag.Normalized;
            if (replacement is null && reference is not null)
            {
                referenceTags ??= ReadTags(reference, category, key, sourceText: true).ToArray();
                replacement = ReferenceRepair(tag, targetTags, referenceTags);
            }
            string token = tag.Raw.Length > 64 ? tag.Raw[..64] + "…" : tag.Raw;
            state.Report(tag.Issue, $"Control '{token}' at character {tag.Start + 1}.", replacement is not null);
            if (replacement is not null)
            {
                edits.Add(new(tag.Start, tag.Length, replacement));
            }
        }
        state.Apply(edits);
    }

    private static string? ReferenceRepair(Tag tag, Tag[] target, Tag[] reference)
    {
        if (tag.Raw.StartsWith('{') && !tag.Raw.EndsWith('}'))
        {
            string closed = tag.Raw + "}";
            return reference.Any(candidate => candidate.Issue is null && candidate.Raw == closed && candidate.Kind != "Field") ? closed : null;
        }
        if (target.Count(candidate => candidate.Issue is not null) != 1)
        {
            return null;
        }

        Tag[] original = reference.Where(candidate => candidate.Dynamic).ToArray();
        Tag[] translated = target.Where(candidate => candidate.Dynamic || candidate.Start == tag.Start).ToArray();
        if (original.Length != translated.Length)
        {
            return null;
        }
        int difference = Array.IndexOf(translated, tag);
        if (difference < 0 || original[difference].Kind != tag.Kind || !OnlyOneCharacterEdit(tag.Raw, original[difference].Raw))
        {
            return null;
        }
        for (int index = 0; index < original.Length; index++)
        {
            if (index != difference && translated[index].Identity != original[index].Identity)
            {
                return null;
            }
        }
        return original[difference].Raw;
    }

    private static bool OnlyOneCharacterEdit(string first, string second)
    {
        if (first.Length > second.Length)
        {
            (first, second) = (second, first);
        }
        if (second.Length - first.Length > 1)
        {
            return false;
        }
        int prefix = 0;
        while (prefix < first.Length && first[prefix] == second[prefix])
        {
            prefix++;
        }
        if (prefix == first.Length)
        {
            return second.Length == first.Length + 1;
        }
        return first.Length == second.Length
            ? first.AsSpan(prefix + 1).SequenceEqual(second.AsSpan(prefix + 1))
            : first.AsSpan(prefix).SequenceEqual(second.AsSpan(prefix + 1));
    }

    private static void ReviewControlContract(ReviewState state, string? source, string category, string key)
    {
        if (TryCompositeSource(category, key, source, out CompositeFormat? composite))
        {
            try
            {
                LocalizationCompositeFormatValidation.Validate(composite!, state.Target, key);
            }
            catch (InvalidDataException exception)
            {
                state.Report("tag-contract", exception.Message);
            }
            return;
        }
        if (source is null || state.Diagnostics.Any(diagnostic => diagnostic.Rule == UnsupportedKey))
        {
            return;
        }
        if (category == Information && (InformationBodyKey().IsMatch(key) || source.Contains("{Info ", StringComparison.Ordinal) || state.Target.Contains("{Info ", StringComparison.Ordinal)))
        {
            if (!NativeTutorialInformationText.ValidateTarget(source, state.Target))
            {
                state.Report("tag-contract", "The Information text must preserve the reference's ordered control stream.");
            }
            return;
        }

        Tag[] sourceTags = ReadTags(source, category, key, sourceText: true).ToArray();
        Tag[] targetTags = ReadTags(state.Target, category, key).ToArray();
        if (sourceTags.Any(tag => tag.Issue is not null) || targetTags.Any(tag => tag.Issue is not null))
        {
            return;
        }

        Dictionary<string, int> required = CountDynamicTags(sourceTags);
        Dictionary<string, int> translated = CountDynamicTags(targetTags);
        var missing = required.Where(pair => translated.GetValueOrDefault(pair.Key) < pair.Value).ToArray();
        var extra = translated.Where(pair => required.GetValueOrDefault(pair.Key) < pair.Value).ToArray();
        if (missing.Length == 1 && extra.Length == 1 && missing[0].Value == 1 && extra[0].Value == 1)
        {
            Tag expected = sourceTags.Single(tag => tag.Identity == missing[0].Key);
            Tag actual = targetTags.Single(tag => tag.Identity == extra[0].Key);
            if (CanReplaceDynamic(expected, actual, sourceTags, targetTags))
            {
                state.Report("tag-incorrect", $"Replace control '{actual.Raw}' with '{expected.Raw}'.", true);
                state.Apply([new(actual.Start, actual.Length, expected.Raw)]);
                return;
            }
        }

        foreach (var pair in missing)
        {
            int count = pair.Value - translated.GetValueOrDefault(pair.Key);
            string raw = sourceTags.First(tag => tag.Identity == pair.Key).Raw;
            state.Report("tag-missing", $"Reference requires {count} more occurrence(s) of '{raw}'.");
        }
        foreach (var pair in extra)
        {
            int count = pair.Value - required.GetValueOrDefault(pair.Key);
            string raw = targetTags.First(tag => tag.Identity == pair.Key).Raw;
            state.Report("tag-extra", $"Target has {count} unexpected occurrence(s) of '{raw}'.");
        }
    }

    private static void ReviewTerminalEnds(ReviewState state, string source)
    {
        int required = CountTerminalEnds(source);
        int translated = CountTerminalEnds(state.Target);
        if (required == 0 || translated <= required || state.Target.Contains(LocalizationValue.InvalidTarget, StringComparison.Ordinal))
        {
            return;
        }
        int excess = translated - required;
        int length = excess * End.Length;
        state.Report("tag-terminal-duplicate", $"Remove {excess} excess terminal End control(s).", true);
        state.Apply([new(state.Target.Length - length, length, "")]);
    }

    private static int CountTerminalEnds(string text)
    {
        int length = text.Length;
        while (text.AsSpan(0, length).EndsWith(End, StringComparison.Ordinal))
        {
            length -= End.Length;
        }
        return (text.Length - length) / End.Length;
    }

    private static bool CanReplaceDynamic(Tag expected, Tag actual, Tag[] sourceTags, Tag[] targetTags)
    {
        // A character reference may be an inflected name or a deliberate change of actor in prose.
        if (expected.Kind != actual.Kind || expected.Kind is not ("Var" or "Item" or "Key" or "Dialog" or "Name"))
        {
            return false;
        }
        string?[] sourceOrder = sourceTags.Where(tag => tag.Dynamic).Select(tag => tag.Identity).ToArray();
        string?[] targetOrder = targetTags.Where(tag => tag.Dynamic).Select(tag => tag.Start == actual.Start ? expected.Identity : tag.Identity).ToArray();
        return sourceOrder.SequenceEqual(targetOrder, StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CountDynamicTags(IEnumerable<Tag> tags)
    {
        return tags.Where(tag => tag.Dynamic).GroupBy(tag => tag.Identity!, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static bool TryCompositeSource(string category, string key, string? reference, out CompositeFormat? format)
    {
        format = null;
        if (category != "System")
        {
            return false;
        }
        return (SystemSources.GetValueOrDefault(key) ?? reference) is string source && LocalizationCompositeFormatValidation.TryReadSource(source, out format);
    }

    private static IEnumerable<Tag> ReadTags(string text, string category, string key, bool sourceText = false)
    {
        bool field = category == "Fields";
        string? nativeLanguage = null;
        int separator = key.IndexOf('_');
        if (!sourceText && key.StartsWith('$') && separator > 1)
        {
            nativeLanguage = key[1..separator].ToUpperInvariant();
            if (nativeLanguage == "JA")
            {
                nativeLanguage = "JP";
            }
        }

        foreach (Match match in BraceTokens().Matches(text))
        {
            string raw = match.Value;
            if (!raw.EndsWith('}') || raw == "}")
            {
                yield return new(match.Index, match.Length, raw, "", null, null, "tag-malformed");
                continue;
            }
            string interior = raw[1..^1].Trim();
            int space = interior.AsSpan().IndexOfAny(' ', '\t');
            string name = space < 0 ? interior : interior[..space];
            string parameter = space < 0 ? "" : interior[(space + 1)..].Trim();
            if (field && name.Length > 1 && name[0] is 'X' or 'W' or 'F' && (char.IsAsciiDigit(name[1]) || name[1] == '-'))
            {
                yield return new(match.Index, match.Length, raw, "Field", null, null, ValidPresentation(text, match) ? null : "tag-malformed");
                continue;
            }
            if (!NativeTagNames.TryGetValue(name, out string? kind) || kind == "Info" && category != Information)
            {
                yield return new(match.Index, match.Length, raw, name, null, null, "tag-unknown");
                continue;
            }
            bool valid = TryNormalizeTag(kind, parameter, nativeLanguage, out string normalized, out string? identity);
            if (field && valid && kind is "Char" or "Name" or "Color")
            {
                valid = kind switch
                {
                    "Char" => Enum.TryParse(normalized, out TextTagCharacter character) && Enum.IsDefined(character) && character is not (TextTagCharacter.UnknownCharacter2 or TextTagCharacter.UnknownCharacter4),
                    "Name" => Enum.TryParse(normalized, out TextTagName namedCharacter) && Enum.IsDefined(namedCharacter),
                    _ => Enum.TryParse(normalized, out TextTagColor color) && Enum.IsDefined(color)
                };
            }
            normalized = parameter.Length == 0 ? $"{{{kind}}}" : $"{{{kind} {normalized}}}";
            if (!valid || match.Length > 64)
            {
                yield return new(match.Index, match.Length, raw, kind, null, null, "tag-malformed");
                continue;
            }
            if (identity is not null && kind == "Key" && Enum.TryParse(parameter, ignoreCase: true, out TextTagKey keyOperand) && !NativeTextKeys.IsSupported(keyOperand))
            {
                yield return new(match.Index, match.Length, raw, kind, null, null, UnsupportedKey);
                continue;
            }
            if (kind == "Empty" && text != LocalizationValue.EmptyTarget)
            {
                yield return new(match.Index, match.Length, raw, kind, null, null, "tag-malformed");
                continue;
            }
            bool canonical = string.Equals(raw, normalized, StringComparison.Ordinal);
            yield return new(match.Index, match.Length, raw, kind, identity, canonical ? null : normalized, canonical ? null : "tag-malformed");
        }
    }

    private static bool ValidPresentation(string text, Match match)
    {
        if (match.Index != 0 || !FieldMessageContext.TryParseTag(0, text, out FieldMessageContext? context, out int length) || length != match.Length)
        {
            return false;
        }
        if (context?.Size is { } size && (size.Width <= 0 || size.Height <= 0))
        {
            return false;
        }
        return context?.Choices is not { } choice || choice.FirstLine >= 0 && choice.LastLine >= choice.FirstLine && choice.LastLine < LocalizationCatalog.CountAuthoredLines(text[length..]);
    }

    private static bool TryNormalizeTag(string kind, string parameter, string? nativeLanguage, out string normalized, out string? identity)
    {
        normalized = parameter;
        identity = null;
        byte value = 0;
        bool valid = kind switch
        {
            "End" or "Next" or "Line" or "Page" or "Speaker" or "Invalid" or "Empty" => parameter.Length == 0,
            "Var" or "Item" or "Pause" or "Option" or "Speed" => TryByte(parameter, out value),
            "Glyph" => TryByte(parameter, out value) && value is >= 204 and <= 226,
            "Char" => TryEnum<TextTagCharacter>(parameter, out normalized, out value),
            "Key" => TryEnum<TextTagKey>(parameter, out normalized, out value),
            "Color" => TryEnum<TextTagColor>(parameter, out normalized, out value),
            "Dialog" => TryEnum<TextTagDialog>(parameter, out normalized, out value),
            "Name" => TryEnum<TextTagName>(parameter, out normalized, out value),
            "Info" => InfoOperand().IsMatch(parameter),
            _ => TextTagTerms.TryGetLanguage(kind, out FF8TextLanguage language) && (nativeLanguage is null || nativeLanguage == language.ToString()) && TextTagTerms.TryParse(language, parameter, out value)
        };
        if (valid && kind is "Var" or "Item" or "Char" or "Key" or "Dialog" or "Name")
        {
            identity = $"{kind} {value.ToString(CultureInfo.InvariantCulture)}";
        }
        return valid;
    }

    private static bool TryEnum<T>(string parameter, out string normalized, out byte numeric) where T : struct, Enum
    {
        normalized = parameter;
        numeric = 0;
        if (parameter.Contains(',') || !Enum.TryParse(parameter, ignoreCase: true, out T value))
        {
            return false;
        }
        numeric = Convert.ToByte(value, CultureInfo.InvariantCulture);
        if (!TryByte(parameter, out _))
        {
            normalized = value.ToString();
        }
        return true;
    }

    private static bool TryByte(string parameter, out byte value)
    {
        return byte.TryParse(parameter, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    [GeneratedRegex(@"\{[^{}\r\n]*(?:\}|(?=[{\r\n]|$))|\}", RegexOptions.CultureInvariant)]
    private static partial Regex BraceTokens();

    [GeneratedRegex(@"\A(?:Gate [0-9AB][0-9A-F]|Number [C-F][0-9A-F]|Link [0-9A-F]{4})\z", RegexOptions.CultureInvariant)]
    private static partial Regex InfoOperand();

    [GeneratedRegex(@"\A\$[a-z]+_mngrp_slot(?:12[89]|13[0-3])_block[0-9]{4}_0001\z", RegexOptions.CultureInvariant)]
    private static partial Regex InformationBodyKey();

    [GeneratedRegex(@"\G\p{IsCyrillic}", RegexOptions.CultureInvariant)]
    private static partial Regex FollowingNameAffix();
}
