using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
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
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        byte[] bytes = Read(menu, "mthomas.bin");
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (bytes.Length != 16 || hash != "CD2C7261EE28D2D6D13DB5922D447FA49D337B07A706BC042D3803E562F62BDD" || bytes[12] != 0)
        {
            throw new NotSupportedException("Solomon Ring ingredients differ from the original resource.");
        }
        byte[] items = Read(menu, "mitem.bin");
        int itemId = Enumerable.Range(1, items.Length / 4 - 1).Single(id => items[id * 4] == 15);
        NativeSolomonRingDefinition definition = new(NativeExecutableHash!, hash, checked((byte)itemId), Enumerable.Range(0, 3).Select(index => new NativeItemStock(bytes[index * 4], bytes[index * 4 + 1])).ToImmutableArray());
        definition.Validate();
        return nativeSolomonRing = definition;
    }
}
