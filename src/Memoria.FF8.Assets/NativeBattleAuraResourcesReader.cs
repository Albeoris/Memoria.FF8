using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAuraResourcesReader
{
    public static NativeBattleAuraResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource layout = new(0x12DB9E0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x12DB9E0, 0x43BC));
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x34C].ToImmutableArray()).Initialize7016B0(layout, 0x34C);
        ImmutableArray<byte> flags = NativeBattleEffectStaticResourcesReader.Read(memory, 0x12E061C, 40);
        return new(new("FF8_EN:12DFD9C", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12DFD9C, 0x87C)), layout, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x12DB818u), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x12DB75C), Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(flags.AsSpan().Slice(index * 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12E0618, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAuraResources? nativeAuraResources;

    public NativeBattleAuraResources ReadNativeAuraResources()
    {
        return nativeAuraResources ??= NativeBattleAuraResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
