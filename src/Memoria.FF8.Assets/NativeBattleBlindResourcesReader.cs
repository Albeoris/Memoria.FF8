using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlindResourcesReader
{
    public static NativeBattleBlindResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource layout = new(0x12726A0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x12726A0, 0x4760));
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x820].ToImmutableArray()).Initialize7016B0(layout, 0x820);
        return new(new("FF8_EN:1276E00", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1276E00, 0x830)), layout, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1277630, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlindResources? nativeBlindResources;

    public NativeBattleBlindResources ReadNativeBlindResources()
    {
        return nativeBlindResources ??= NativeBattleBlindResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
