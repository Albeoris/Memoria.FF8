using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRapture288ResourcesReader
{
    public static NativeBattleRapture288Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<uint> colors = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1078980u + (uint)index * 4), 4))).ToImmutableArray();
        byte[] vector = memory.Read(0x1078978, 8);
        return new(new(0x10749A8, NativeBattleEffectStaticResourcesReader.Read(memory, 0x10749A8, 0xE58)), new(0x1075800, NativeBattleEffectStaticResourcesReader.Read(memory, 0x1075800, 0x2FF0)), new(0, 0x1074658, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1074658)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10747B0), new("FF8_EN:10787F0", NativeBattleEffectStaticResourcesReader.Read(memory, 0x10787F0, 0x180)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1078970, 4)), new(BinaryPrimitives.ReadInt16LittleEndian(vector), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(4))), colors);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRapture288Resources? nativeRapture288Resources;
    public NativeBattleRapture288Resources ReadNativeRapture288Resources() => nativeRapture288Resources ??= NativeBattleRapture288ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
