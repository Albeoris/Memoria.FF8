using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRecover93ResourcesReader
{
    public static NativeBattleRecover93Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x1538B38, 0x4694);
        int[] sections = [0x18, 0x688, 0x3344, 0x4688, 0x4694];
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native Recover model requires its original four-section source.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Recover model section disagrees with its complete original extent.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x1538B38, bytes, pose, geometry);
        NativeBattleGfAnimationSource program = new(0x16B8480, NativeBattleEffectStaticResourcesReader.Read(memory, 0x16B8480, 0x510));
        if (U32(0) != 2 || U32(4) != 0x1B8 || U32(8) != 0x10 || U32(12) != 0xE4)
        {
            throw new InvalidDataException("Native Recover program requires its original bounded geometry and animation directories.");
        }
        foreach (int offset in new int[] { 0x10, 0xE4 })
        {
            if (U32(offset) != 0x54 || U32(offset + 4) != 9 || U32(offset + 8) != 1)
            {
                throw new InvalidDataException("Native Recover program requires its original12-byte single-shape geometry headers.");
            }
        }
        return new(new("FF8_EN:1538728", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1538728, 0x410)), model, new("FF8_EN:16B8990", NativeBattleEffectStaticResourcesReader.Read(memory, 0x16B8990, 0x8420)), Sound(0xB8B9A8), NativeBattleRecover93NodeResourcesReader.Read(executable), program, Sound(0x1538720), Sound(0x1538724), Sound(0x16C0DB0), Sound(0x16C0DB4));

        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(program.Data.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRecover93Resources? nativeRecover93Resources;
    public NativeBattleRecover93Resources ReadNativeRecover93Resources() => nativeRecover93Resources ??= NativeBattleRecover93ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
