using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Runtime;

public sealed record NativeMessageExpansionSegment(int SourceStart, int SourceLength, string Text, bool IsReplacement);

/// <summary>The eight process-owned DWORDs at native1D2B530. Null is unknown history, never zero.</summary>
public sealed partial record NativeMessageVariables([property: JsonRequired] int? Slot0, [property: JsonRequired] int? Slot1, [property: JsonRequired] int? Slot2, [property: JsonRequired] int? Slot3, [property: JsonRequired] int? Slot4, [property: JsonRequired] int? Slot5, [property: JsonRequired] int? Slot6, [property: JsonRequired] int? Slot7)
{
    public static NativeMessageVariables FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0);
    public static NativeMessageVariables Unknown() => new(null, null, null, null, null, null, null, null);
    [JsonIgnore]
    public bool IsColdProcessBank => this == FromColdProcess();

    public int? At(int slot) => slot switch
    {
        0 => Slot0,
        1 => Slot1,
        2 => Slot2,
        3 => Slot3,
        4 => Slot4,
        5 => Slot5,
        6 => Slot6,
        7 => Slot7,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public NativeMessageVariables WithWrite(int slot, int value) => slot switch
    {
        0 => this with { Slot0 = value },
        1 => this with { Slot1 = value },
        2 => this with { Slot2 = value },
        3 => this with { Slot3 = value },
        4 => this with { Slot4 = value },
        5 => this with { Slot5 = value },
        6 => this with { Slot6 = value },
        7 => this with { Slot7 = value },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string Format(byte parameter, NativeMessageVariables? bank)
    {
        if (parameter is not (>= 32 and <= 39 or >= 48 and <= 55 or >= 64 and <= 71))
        {
            return "";
        }
        int slot = parameter & 7;
        uint value = unchecked((uint)(bank?.At(slot) ?? throw new NotSupportedException($"Message variable slot{slot} has no known process history.")));
        // Original English kernel numeric alphabet55/11 proves digits0..9, A..F and comma.
        return parameter < 40 ? value.ToString("N0", CultureInfo.InvariantCulture) : parameter < 56 ? value.ToString(CultureInfo.InvariantCulture) : value.ToString("X8", CultureInfo.InvariantCulture);
    }

    public static string Expand(string template, NativeMessageVariables? bank)
    {
        return VariableTag().Replace(template, match => ExpandVariable(match, bank));
    }

    public static IReadOnlyList<NativeMessageExpansionSegment> ExpandSegments(string template, NativeMessageVariables? bank)
    {
        List<NativeMessageExpansionSegment> segments = [];
        int cursor = 0;
        foreach (Match match in VariableTag().Matches(template))
        {
            if (match.Index > cursor)
            {
                segments.Add(new(cursor, match.Index - cursor, template[cursor..match.Index], false));
            }
            segments.Add(new(match.Index, match.Length, ExpandVariable(match, bank), true));
            cursor = match.Index + match.Length;
        }
        if (cursor < template.Length)
        {
            segments.Add(new(cursor, template.Length - cursor, template[cursor..], false));
        }
        return segments;
    }

    private static string ExpandVariable(Match match, NativeMessageVariables? bank)
    {
        string digits = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        if (!byte.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out byte parameter))
        {
            throw new NotSupportedException("Message variable tag requires its original BYTE parameter.");
        }
        return Format(parameter, bank);
    }

    [GeneratedRegex(@"\{Var(?:\s+(\d+)|(\d+))\}")]
    private static partial Regex VariableTag();
}
