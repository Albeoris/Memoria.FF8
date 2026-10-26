using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Assets;

/// <summary>Unicode prose with the Information interpreter's immutable control stream.</summary>
public static class NativeTutorialInformationText
{
    private static readonly Regex Controls = new("\\{[^{}]*\\}|\\r\\n|\\r|\\n", RegexOptions.CultureInvariant);

    public static string Format(ReadOnlySpan<byte> body, Encoding encoding, bool japanese = false)
    {
        StringBuilder result = new();
        int start = 0;
        for (int position = 0; position < body.Length; position++)
        {
            byte value = body[position];
            if (value is 0x0A or 0x0B)
            {
                int width = value == 0x0A ? 2 : 3;
                if (position + width > body.Length)
                {
                    throw new InvalidDataException("Information control operand is truncated.");
                }
                result.Append(encoding.GetString(body[start..position].ToArray()));
                if (value == 0x0A)
                {
                    result.Append("{Info ").Append(body[position + 1] < 0xC0 ? "Gate " : "Number ").Append(body[position + 1].ToString("X2", CultureInfo.InvariantCulture)).Append('}');
                }
                else
                {
                    result.Append("{Info Link ").Append(body[position + 1].ToString("X2", CultureInfo.InvariantCulture)).Append(body[position + 2].ToString("X2", CultureInfo.InvariantCulture)).Append('}');
                }
                position += width - 1;
                start = position + 1;
            }
            else if (value is >= 4 and <= 15 || japanese && value is >= 0x19 and <= 0x1C)
            {
                if (++position >= body.Length)
                {
                    throw new InvalidDataException("Information text operand is truncated.");
                }
            }
        }
        result.Append(encoding.GetString(body[start..].ToArray()));
        return result.ToString();
    }

    public static bool ValidateTarget(string source, string target)
    {
        if (target.Any(character => char.IsControl(character) && character is not '\r' and not '\n'))
        {
            return false;
        }
        return Controls.Matches(source).Select(match => match.Value).SequenceEqual(Controls.Matches(target).Select(match => match.Value), StringComparer.Ordinal)
            && !Controls.Replace(target, "").Any(character => character is '{' or '}');
    }

    internal static IEnumerable<(string Text, bool Control)> Parts(string text)
    {
        int start = 0;
        foreach (Match match in Controls.Matches(text))
        {
            if (match.Index > start)
            {
                yield return (text[start..match.Index], false);
            }
            yield return (match.Value, true);
            start = match.Index + match.Length;
        }
        if (start < text.Length)
        {
            yield return (text[start..], false);
        }
    }
}
