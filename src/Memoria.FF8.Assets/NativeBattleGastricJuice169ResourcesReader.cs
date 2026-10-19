using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGastricJuice169ResourcesReader
{
    public static NativeBattleGastricJuice169Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD6A8C4).Bytes;
        ImmutableArray<byte> puddle = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD6B534).Bytes;
        if (mesh.Length != 0xA20 || puddle.Length != 0x6E8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(4)) != 73)
        {
            throw new InvalidDataException("Native Gastric Juice requires its complete original mesh and puddle sources.");
        }
        ImmutableArray<byte> counts = NativeBattleEffectStaticResourcesReader.Read(memory, 0xD6BC24, 12 * 4);
        return new(mesh, NativeBattleEffectStaticResourcesReader.Read(memory, 0xD6B2EC, 73 * 8), puddle, new uint[] { 0xD6A544, 0xD6A620, 0xD6A718 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address)).ToImmutableArray(), new("FF8_EN:D6A3F8", NativeBattleEffectStaticResourcesReader.Read(memory, 0xD6A3F8, 0x14C)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD6BC1C, 4)), Enumerable.Range(0, 12).Select(index => BinaryPrimitives.ReadInt32LittleEndian(counts.AsSpan().Slice(index * 4))).ToImmutableArray());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGastricJuice169Resources? nativeGastricJuice169Resources;
    public NativeBattleGastricJuice169Resources ReadNativeGastricJuice169Resources() => nativeGastricJuice169Resources ??= NativeBattleGastricJuice169ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
