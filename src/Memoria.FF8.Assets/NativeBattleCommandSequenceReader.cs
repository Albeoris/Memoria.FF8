using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCommandSequenceReader
{
    public static ImmutableArray<byte> Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static ImmutableArray<byte> Read(IGameNativeMemorySource memory)
    {
        const int count = 0x27;
        //48D200 indexes B8150C only for physical actors0..2 with commandBYTE<27h; packet+2 owns the selected BYTE.
        return memory.Read(0xB8150C, count).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<byte> nativeBattleCommandSequences;

    public ImmutableArray<byte> ReadNativeBattleCommandSequences()
    {
        if (nativeBattleCommandSequences.IsDefault)
        {
            nativeBattleCommandSequences = NativeBattleCommandSequenceReader.Read(GameDataResources.ReadNativeMemory(this.resources));
        }
        return nativeBattleCommandSequences;
    }
}
