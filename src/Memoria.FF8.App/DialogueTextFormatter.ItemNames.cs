using System.Globalization;
using System.Text.RegularExpressions;

namespace Memoria.FF8;

internal static partial class DialogueTextFormatter
{
    internal static string ExpandItemNames(string template, Func<int, string?> itemName)
    {
        return ItemNameTag().Replace(template, match =>
        {
            if (!byte.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out byte operand) || operand < 32)
            {
                throw new InvalidDataException("An item-name tag requires the original BYTE operand in the native item range.");
            }
            int selector = operand - 32;
            return itemName(selector) ?? throw new InvalidDataException($"No original item name exists for selector {selector}.");
        });
    }

    [GeneratedRegex(@"\{Item\s+(\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ItemNameTag();
}
