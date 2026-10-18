using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSearchModelResourcesReader
{
    public static NativeBattleSearchModelResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x153D59C, 0x5B38);
        int[] expected = [0x18, 0x688, 0x3344, 0x5B2C, 0x5B38];
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native effect-owned model requires its original four-section resource.");
        }
        for (int section = 0; section < expected.Length; section++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), section) != expected[section])
            {
                throw new InvalidDataException("Native effect-owned model section extent disagrees with its complete original resource.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(expected[0], expected[1] - expected[0]), bytes.Slice(expected[2], expected[3] - expected[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(expected[1], expected[2] - expected[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x153D59C, bytes, pose, geometry);
        return new(model, new("FF8_EN:16C12C8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x16C12C8, 0x8420)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSearchModelResources? nativeSearchModelResources;
    public NativeBattleSearchModelResources ReadNativeSearchModelResources() => nativeSearchModelResources ??= NativeBattleSearchModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
