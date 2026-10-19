using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleUltraWaves154ResourcesReader
{
    public static NativeBattleUltraWaves154Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] vectors = memory.Read(0xDBADF0, 64);
        ImmutableArray<NativeBattleVector> emitters = Enumerable.Range(0, 8).Select(index => new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(vectors.AsSpan(index * 8)), BinaryPrimitives.ReadInt16LittleEndian(vectors.AsSpan(index * 8 + 2)), BinaryPrimitives.ReadInt16LittleEndian(vectors.AsSpan(index * 8 + 4)))).ToImmutableArray();
        return new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDBAAC8u), new("FF8_EN:DBA910", memory.Read(0xDBA910, 0x1B8).ToImmutableArray()), U32(0xDBAD38), Enumerable.Range(0, 35).Select(index => U32(0xDBAD40u + (uint)index * 4)).ToImmutableArray(), Enumerable.Range(0, 16).Select(index => U32(0xDBAE30u + (uint)index * 4)).ToImmutableArray(), emitters, Enumerable.Range(0, 8).Select(index => unchecked((int)U32(0xDBAE70u + (uint)index * 4))).ToImmutableArray(), memory.Read(0xDBAEE0, 48).ToImmutableArray());

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleUltraWaves154Resources? nativeUltraWaves154Resources;
    public NativeBattleUltraWaves154Resources ReadNativeUltraWaves154Resources() => nativeUltraWaves154Resources ??= NativeBattleUltraWaves154ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
