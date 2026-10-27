using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeSolomonRingDefinition? nativeSolomonRing;

    public NativeSolomonRingDefinition ReadNativeSolomonRingDefinition()
    {
        if (nativeSolomonRing is not null)
        {
            return nativeSolomonRing;
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Solomon Ring requires the authenticated original executable.");
        }
        using IFlArchive menu = OpenArchive("menu");
        byte[] bytes = Read(menu, "mthomas.bin");
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        NativeMenuDataReader.ValidateSolomonRingIngredients(bytes);
        byte[] items = Read(menu, "mitem.bin");
        byte itemId = NativeMenuDataReader.ReadSolomonRingItemId(items);
        NativeSolomonRingDefinition definition = new(NativeExecutableHash!, hash, itemId, Enumerable.Range(0, 3).Select(index => new NativeItemStock(bytes[index * 4], bytes[index * 4 + 1])).ToImmutableArray());
        definition.Validate();
        return nativeSolomonRing = definition;
    }
}
