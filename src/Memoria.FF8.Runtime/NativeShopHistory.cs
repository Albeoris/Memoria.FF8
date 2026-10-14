using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Twenty saved20-byte shop rows at1CFE5A8, copied from the actual New Game template.</summary>
public sealed record NativeShopHistory([property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        if (InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || Bytes.IsDefault || Bytes.Length != 400)
        {
            throw new InvalidDataException("Native shop history requires all twenty original saved rows and initialization identity.");
        }
    }

    public NativeShopHistory Refresh(NativeShopDefinitions definitions, int catalog, uint abilityMask)
    {
        Validate();
        definitions.Validate();
        if ((uint)catalog >= definitions.Catalogs.Length)
        {
            throw new NotSupportedException("Native shop catalog is outside the original twenty rows.");
        }
        ImmutableArray<byte>.Builder bytes = Bytes.ToBuilder();
        for (int slot = 0; slot < 16; slot++)
        {
            NativeShopCatalogEntry entry = definitions.Catalogs[catalog][slot];
            if (entry.ItemId != 0)
            {
                int threshold = entry.Availability + ((abilityMask & 4) != 0 ? 256 : 0);
                if (threshold == 255)
                {
                    threshold = 256;
                }
                bytes[20 * catalog + slot] = threshold > 128 ? (byte)1 : (byte)0;
            }
        }
        bytes[20 * catalog + 16] = 1;
        bytes[20 * catalog + 17] = 0;
        return this with { Bytes = bytes.ToImmutable() };
    }
}
