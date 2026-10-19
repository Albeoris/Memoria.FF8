using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleUltrasonic152ResourcesReader
{
    public static NativeBattleUltrasonic152Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> ring = NativeBattleEffectStaticResourcesReader.Read(memory, 0xDBE9E0, 0x270);
        ImmutableArray<uint> colors = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDBEC58u + (uint)index * 4, 4))).ToImmutableArray();
        return new(ring, [NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDBE5CC), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDBE420), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDBE814), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDBE904)], colors, new("FF8_EN:DBE370", NativeBattleEffectStaticResourcesReader.Read(memory, 0xDBE370, 0xB0)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDBEC50, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleUltrasonic152Resources? nativeUltrasonic152Resources;
    public NativeBattleUltrasonic152Resources ReadNativeUltrasonic152Resources() => nativeUltrasonic152Resources ??= NativeBattleUltrasonic152ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
