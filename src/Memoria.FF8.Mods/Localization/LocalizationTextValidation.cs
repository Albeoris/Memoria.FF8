using System.Globalization;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static class LocalizationTextValidation
{
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
        Dictionary<string, int> translated = Placeholders(row.Target, row.Key);
        if (row.SourcePresent && row.Source is not null)
        {
            Dictionary<string, int> required = Placeholders(row.Source, row.Key);
            if (required.Count != translated.Count || required.Any(pair => translated.GetValueOrDefault(pair.Key) != pair.Value))
            {
                throw new InvalidDataException($"{row.Key}: translated text must preserve the required dynamic placeholders and their occurrence counts.");
            }
        }
    }

    private static Dictionary<string, int> Placeholders(string text, string key)
    {
        Dictionary<string, int> placeholders = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(text, "\\{(End|Next|Line|Speaker|Pause|Var|Char|Key|Color|Dialog|Option|Term|Name)(?=[ }]|$)", RegexOptions.CultureInvariant))
        {
            int close = text.IndexOf('}', match.Index + 1);
            if (close < 0 || close - match.Index + 1 > 32)
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
                case "Term":
                    valid = Parse<TextTagTerm>(parameter, out number) && hasSpace;
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
            if (code is "Var" or "Char" or "Key" or "Dialog" or "Term" or "Name")
            {
                string identity = code + " " + number;
                placeholders[identity] = placeholders.GetValueOrDefault(identity) + 1;
            }
        }
        return placeholders;
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
