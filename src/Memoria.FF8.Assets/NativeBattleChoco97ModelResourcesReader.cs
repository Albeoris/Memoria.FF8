using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97ModelResourcesReader
{
    public static NativeBattleChoco97ModelResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint address = 0x168354C;
        ImmutableArray<byte> bytes = memory.Read(address, 0x4EE8).ToImmutableArray();
        int[] offsets = [0x18, 0x688, 0x39FC, 0x4EDC, 0x4EE8];
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native Choco model requires its complete original four-section resource.");
        }
        for (int index = 0; index < offsets.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != offsets[index])
            {
                throw new InvalidDataException("Native Choco model section extent contradicts its original resource.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(offsets[0], offsets[1] - offsets[0]), bytes.Slice(offsets[2], offsets[3] - offsets[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(offsets[1], offsets[2] - offsets[1]).ToArray(), pose.BoneCount);
        if (pose.BoneCount != 34 || NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()).Length != 6 || geometry.Length != 1 || geometry[0].Vertices.Length != 410)
        {
            throw new InvalidDataException("Native Choco model requires its actual skeleton, poses and single geometry object.");
        }
        geometry[0].Validate();
        NativeBattleEffectOwnedModelResource model = new(address, bytes, pose, geometry);
        return new(model, new("FF8_EN:1688434", memory.Read(0x1688434, 0x4220).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4)), new(0x152BB58, memory.Read(0x152BB58, 32).ToImmutableArray()), new(address + (uint)offsets[3], bytes.Slice(offsets[3], offsets[4] - offsets[3])));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ModelResources? nativeChoco97Model;

    public NativeBattleChoco97ModelResources ReadNativeChoco97ModelResources() => nativeChoco97Model ??= NativeBattleChoco97ModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
