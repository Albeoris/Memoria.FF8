using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    public NativeChocoboWorldTransferTable ReadNativeChocoboWorldTransferTable()
    {
        using IFlArchive menu = OpenArchive("menu");
        NativeChocoboWorldTransferTable result = new(ImmutableArray.CreateRange(Read(menu, "cyocobo.bin")));
        result.Validate();
        return result;
    }
}
