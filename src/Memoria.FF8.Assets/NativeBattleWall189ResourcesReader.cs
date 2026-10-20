using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWall189ResourcesReader
{
    public static NativeBattleWall189Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0, 0xD19658, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xD19658)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD19804), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD19ABC), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD19B74), new("FF8_EN:D19580", NativeBattleEffectStaticResourcesReader.Read(memory, 0xD19580, 0xD8)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD1B3A4, 4)), NativeBattleEffectStaticResourcesReader.Read(memory, 0xD1B490, 24), Enumerable.Range(0, 56).Select(index => BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(0xD1B3ACu + (uint)index * 4), 4))).ToImmutableArray());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWall189Resources? nativeWall189Resources;
    public NativeBattleWall189Resources ReadNativeWall189Resources() => nativeWall189Resources ??= NativeBattleWall189ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
