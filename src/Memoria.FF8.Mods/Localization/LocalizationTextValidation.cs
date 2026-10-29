using System.Globalization;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static class LocalizationTextValidation
{
    private static readonly IReadOnlyDictionary<string, string> SystemSources = SystemTextCatalog.SourceEntries().ToDictionary(entry => entry.Key, entry => entry.Text, StringComparer.Ordinal);

    public static void Validate(LocalizationRecord row)
    {
        if (string.IsNullOrEmpty(row.Target))
        {
            return;
        }
        if (row.Target.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            throw new InvalidDataException($"{row.Key}: translated text contains a raw control character.");
        }
        string? source = row.Category == "System" && row.File == SystemTextCatalog.File && SystemSources.TryGetValue(row.Key, out string? authored) ? authored : row.SourcePresent ? row.Source : null;
        if (row.Category == "System" && LocalizationCompositeFormatValidation.TryReadSource(source ?? "", out var composite))
        {
            LocalizationCompositeFormatValidation.Validate(composite!, row.Target, row.Key);
            return;
        }
        Dictionary<string, int> translated = Placeholders(row.Target, row.Key, out _);
        if (source is not null)
        {
            Dictionary<string, int> required = Placeholders(source, row.Key, out bool nativeSource);
            if (row.Category == "System" && !nativeSource)
            {
                throw new InvalidDataException($"{row.Key}: the System source is neither a valid BCL template nor a recognized native-control owner.");
            }
            if (required.Count != translated.Count || required.Any(pair => translated.GetValueOrDefault(pair.Key) != pair.Value))
            {
                throw new InvalidDataException($"{row.Key}: translated text must preserve the required dynamic placeholders and their occurrence counts.");
            }
        }
    }

    private static Dictionary<string, int> Placeholders(string text, string key, out bool containsNativeControls)
    {
        containsNativeControls = false;
        Dictionary<string, int> placeholders = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(text, "\\{(End|Next|Line|Speaker|Pause|Var|Item|Char|Key|Color|Dialog|Option|Term[^ {}]*|Name)(?=[ }]|$)", RegexOptions.CultureInvariant))
        {
            int close = text.IndexOf('}', match.Index + 1);
            if (close < 0 || close - match.Index + 1 > 64)
            {
                throw new InvalidDataException($"{key}: malformed known control tag near character {match.Index}.");
            }
            string code = match.Groups[1].Value;
            string parameter = text[(match.Index + match.Length)..close];
            bool hasSpace = parameter.StartsWith(' ');
            if (hasSpace)
            {
                parameter = parameter[1..];
            }
            bool valid;
            byte number = 0;
            switch (code)
            {
                case "End":
                case "Next":
                case "Line":
                case "Speaker":
                    valid = parameter.Length == 0 && !hasSpace;
                    break;
                case "Var":
                case "Item":
                case "Pause":
                case "Option":
                    valid = hasSpace && byte.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
                    break;
                case "Char":
                    valid = Parse<TextTagCharacter>(parameter, out number) && hasSpace;
                    break;
                case "Key":
                    valid = Parse<TextTagKey>(parameter, out number) && hasSpace;
                    break;
                case "Color":
                    valid = Parse<TextTagColor>(parameter, out number) && hasSpace;
                    break;
                case "Dialog":
                    valid = Parse<TextTagDialog>(parameter, out number) && hasSpace;
                    break;
                case string term when term.StartsWith("Term", StringComparison.Ordinal):
                    valid = TextTagTerms.TryGetLanguage(term, out FF8TextLanguage language) && hasSpace && TextTagTerms.TryParse(language, parameter, out number);
                    if (valid && NativeKeyLanguage(key) is string nativeLanguage)
                    {
                        valid = language == FF8TextLanguages.Parse(nativeLanguage);
                    }
                    break;
                case "Name":
                    valid = Parse<TextTagName>(parameter, out number) && hasSpace;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
            {
                throw new InvalidDataException($"{key}: malformed known control tag {{{code}{(hasSpace ? " " : "")}{parameter}}}.");
            }
            containsNativeControls = true;
            if (code is "Var" or "Item" or "Char" or "Key" or "Dialog" or "Name")
            {
                string identity = code + " " + number;
                placeholders[identity] = placeholders.GetValueOrDefault(identity) + 1;
            }
        }
        return placeholders;
    }

    private static string? NativeKeyLanguage(string key)
    {
        int end = key.IndexOf('_');
        return key.StartsWith('$') && end > 1 ? key[1..end] : null;
    }

    private static bool Parse<T>(string text, out byte value) where T : struct, Enum
    {
        if (Enum.TryParse(text, out T parsed))
        {
            value = System.Convert.ToByte(parsed, CultureInfo.InvariantCulture);
            return true;
        }
        value = 0;
        return false;
    }
}
