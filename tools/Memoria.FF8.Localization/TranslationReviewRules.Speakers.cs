using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal static partial class TranslationReviewRules
{
    private static string ReviewSpeakers(ReviewState state, string source, IReadOnlyDictionary<string, string>? characterNames, IReadOnlyDictionary<string, string>? partyAliases)
    {
        IReadOnlyList<FieldSpeakerLabels.Page> sourcePages = FieldSpeakerLabels.Read(source);
        IReadOnlyList<FieldSpeakerLabels.Page> targetPages = FieldSpeakerLabels.Read(state.Target);
        if (sourcePages.Count != targetPages.Count)
        {
            return source;
        }

        List<Edit> targetEdits = [];
        List<Edit> contractEdits = [];
        for (int index = 0; index < sourcePages.Count; index++)
        {
            FieldSpeakerLabels.Label? expected = sourcePages[index].FirstLine;
            FieldSpeakerLabels.Label? translated = targetPages[index].FirstLine;
            if (expected is null || translated is null || !FieldSpeakerLabels.CanApplySpeakerContract(expected, translated))
            {
                continue;
            }

            string sourceName = expected.Text.TrimEnd(':');
            string targetName = translated.Text.TrimEnd(':');
            FieldSpeakerLabels.Character? sourceTag = FieldSpeakerLabels.TaggedCharacter(sourceName);
            FieldSpeakerLabels.Character? character = sourceTag ?? FieldSpeakerLabels.SourceCharacter(sourceName);
            string? approvedAlias = partyAliases?.GetValueOrDefault(targetName);
            if (approvedAlias is not null && character?.Tag != approvedAlias)
            {
                state.Report("speaker-character", $"The approved alias '{targetName}' identifies '{approvedAlias}', but the English speaker is '{expected.Text}'. Verify the speaker identity.");
                continue;
            }
            if (character is null)
            {
                if (LatinLabel().IsMatch(sourceName) && targetName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    if (sourceName.Equals(targetName, StringComparison.Ordinal) && !AcronymLabel().IsMatch(sourceName) && characterNames?.TryGetValue(sourceName, out string? localizedSpeaker) == true)
                    {
                        targetEdits.Add(new(translated.Start, targetName.Length, localizedSpeaker));
                        state.Report("speaker-untranslated", $"Use the attested speaker label '{localizedSpeaker}' for '{translated.Text}'.", true);
                    }
                    else
                    {
                        state.Report("speaker-untranslated", $"Speaker label '{translated.Text}' needs a verified Russian translation.");
                    }
                }
                continue;
            }

            FieldSpeakerLabels.Character? targetTag = FieldSpeakerLabels.TaggedCharacter(targetName);
            if (targetTag is not null && targetTag.Tag != character.Tag)
            {
                state.Report("speaker-character", $"Verify the identity of speaker label '{translated.Text}' against '{expected.Text}'.");
                continue;
            }
            bool sameLiteral = targetName.Equals(sourceName, StringComparison.OrdinalIgnoreCase)
                || approvedAlias is not null
                || FieldSpeakerLabels.LiteralCharacter(targetName)?.Name == character.Name
                || characterNames?.TryGetValue(character.Name, out string? localized) == true && targetName.Equals(localized, StringComparison.OrdinalIgnoreCase);
            if (targetTag is null && !sameLiteral)
            {
                state.Report("speaker-character", $"Verify the identity of speaker label '{translated.Text}' against '{expected.Text}'.");
                continue;
            }

            // Keeping the translated label's colons and styling also preserves the native line boundary.
            string canonical = character.Tag;
            if (targetName != canonical)
            {
                string replacement = canonical + translated.Text[targetName.Length..];
                targetEdits.Add(new(translated.Start, translated.Length, replacement));
                state.Report("speaker-character", $"Use '{canonical}' for speaker label '{translated.Text}'.", true);
            }
            if (sourceTag is null)
            {
                contractEdits.Add(new(expected.Start, sourceName.Length, canonical));
            }
        }
        state.Apply(targetEdits);
        return ApplyEdits(source, contractEdits);
    }

    private static bool IsProvenSpeaker(FieldSpeakerLabels.Label? label)
    {
        return label is not null && (label.HasMarker || label.StartsQuoted);
    }

    public static bool TryGetGlossaryWitness(string target, string english, out string englishLabel, out string russianLabel)
    {
        englishLabel = "";
        russianLabel = "";
        if (ContainsChoiceSyntax(target) || ContainsChoiceSyntax(english) || target.Contains(LocalizationValue.InvalidTarget, StringComparison.Ordinal))
        {
            return false;
        }
        IReadOnlyList<FieldSpeakerLabels.Page> reference = FieldSpeakerLabels.Read(english);
        IReadOnlyList<FieldSpeakerLabels.Page> translated = FieldSpeakerLabels.Read(target);
        if (reference.Count != translated.Count)
        {
            return false;
        }
        for (int index = 0; index < reference.Count; index++)
        {
            FieldSpeakerLabels.Label? original = reference[index].FirstLine;
            FieldSpeakerLabels.Label? localized = translated[index].FirstLine;
            if (!IsProvenSpeaker(original) || !IsProvenSpeaker(localized))
            {
                continue;
            }
            string originalName = original!.Text.TrimEnd(':');
            string localizedName = localized!.Text.TrimEnd(':');
            if (IsGlossaryEntry(originalName, localizedName))
            {
                englishLabel = originalName;
                russianLabel = localizedName;
                return true;
            }
        }
        return false;
    }

    public static bool IsGlossaryEntry(string englishLabel, string russianLabel)
    {
        return englishLabel == englishLabel.Trim() && russianLabel == russianLabel.Trim() &&
            FieldSpeakerLabels.SourceCharacter(englishLabel) is null && !AcronymLabel().IsMatch(englishLabel) &&
            LatinLabel().IsMatch(englishLabel) && CyrillicLabel().IsMatch(russianLabel);
    }

    public static bool IsPartyAlias(string russianLabel, string tag)
    {
        return russianLabel == russianLabel.Trim() && CyrillicLabel().IsMatch(russianLabel) &&
            tag.StartsWith("{Char ", StringComparison.Ordinal) && tag.EndsWith('}') &&
            Enum.TryParse(tag[6..^1], out TextTagCharacter character) && character is >= TextTagCharacter.Squall and <= TextTagCharacter.Ward &&
            tag == $"{{Char {character}}}";
    }

    private static bool ContainsChoiceSyntax(string text)
    {
        return text.Contains("{Option ", StringComparison.Ordinal)
            || FieldMessageContext.TryParseTag(0, text, out FieldMessageContext? context, out _) && context?.Choices is not null;
    }

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9 .’'\-]{0,47}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LatinLabel();

    [GeneratedRegex(@"\A\p{IsCyrillic}[\p{IsCyrillic} .'’\-]{0,47}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CyrillicLabel();

    [GeneratedRegex(@"\A[A-Z][A-Z0-9.]{1,7}\z", RegexOptions.CultureInvariant)]
    private static partial Regex AcronymLabel();
}
