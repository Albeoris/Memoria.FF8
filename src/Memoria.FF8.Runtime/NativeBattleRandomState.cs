using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Original48F050 initialization and48F020: eight BYTE indexes and one selected lane, independent of event/menu RNG.</summary>
public sealed record NativeBattleRandomState([property: JsonRequired] ImmutableArray<byte> Indexes, [property: JsonRequired] byte Lane)
{
    // Authenticated FF8_EN.exe B697F8, NativeFieldRuntimeProfile.FF8EnExecutableHash. This is native data, not an inferred permutation.
    private static readonly byte[] table = Convert.FromHexString("6306F023F8E5A801C1AE7F487BB1DC09226D7DEE9D58D55524397ADF8E546C1BC00BD043D89A475D2102174BDB11AF70CD4D344972912D62975945F76E46AA0AA3C8319238FAD4E6CBF3DE6BBBF11C3CD6ADB2A9DD5742950C79251FBCE7AC5B832876F218DA87A1616FBE5A5E51EFB0C9157489BDD1A275D799854C4FD2BF4A200856A0503A67264133B7BAFB30CF7C842C32E91D168278A480655F0E27B919C3A7B6003BFC88E1C693FE8BD9B813692F641237FD77E2B504E01A8C8FB4CCF960EB29E390A5683D81733FAB7EB30FCEC43594968671D32AE49F9CEC4E14F5EA40A6F60398C507F42BC23EE89B36532E8D0D5210661EED8A449E05FF5CC76ACA");

    public static NativeBattleRandomState Initialize(byte seed)
    {
        byte[] indexes = new byte[8];
        byte value = seed;
        for (int lane = 0; lane < indexes.Length; lane++)
        {
            indexes[lane] = value;
            value = table[indexes[0]];
            indexes[0] = unchecked((byte)(indexes[0] + 1));
        }
        byte selected = (byte)(table[indexes[0]] & 7);
        indexes[0] = unchecked((byte)(indexes[0] + 1));
        return new(indexes.ToImmutableArray(), selected);
    }

    public NativeBattleRandomState Draw(out byte result)
    {
        Validate();
        byte[] indexes = Indexes.ToArray();
        result = table[indexes[Lane]];
        indexes[Lane] = unchecked((byte)(indexes[Lane] + 1));
        return this with { Indexes = indexes.ToImmutableArray() };
    }

    public void Validate()
    {
        if (Indexes.IsDefault || Indexes.Length != 8 || Lane >= 8)
        {
            throw new InvalidDataException("Native battle RNG requires all eight BYTE indexes and its selected lane.");
        }
    }
}
