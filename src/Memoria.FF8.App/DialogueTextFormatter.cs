using System.Text.RegularExpressions;
using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.TextEncoding.Tags;

namespace Memoria.FF8;

internal static partial class DialogueTextFormatter
{
    public static string Format(string encoded, IReadOnlyDictionary<int, string>? names = null, NativeMessageVariables? messageVariables = null)
    {
        return InlineTag().Replace(NativeMessageVariables.Expand(encoded, messageVariables), match =>
        {
            string kind = match.Groups[1].Value;
            string parameter = match.Groups[2].Value;
            switch (kind)
            {
                case "Line":
                case "Page":
                case "Next":
                    return "\n";
                case "End":
                case "Color":
                case "Pause":
                case "Speaker":
                    return "";
                case "Char":
                    if (Enum.TryParse(parameter, out TextTagCharacter character) && Enum.IsDefined(character))
                    {
                        int identity = (byte)character - (byte)TextTagCharacter.Squall;
                        if (identity >= 0 && identity <= 10 && names?.TryGetValue(identity, out string? customName) == true)
                        {
                            return customName;
                        }
                        return character is TextTagCharacter.Angelo1 or TextTagCharacter.Angelo2 ? "Angelo" : character.ToString();
                    }
                    return parameter;
                default:
                    return match.Value;
            }
        });
    }

    [GeneratedRegex(@"\{([^\s}]+)(?:\s+([^}]+))?\}")]
    private static partial Regex InlineTag();
}
