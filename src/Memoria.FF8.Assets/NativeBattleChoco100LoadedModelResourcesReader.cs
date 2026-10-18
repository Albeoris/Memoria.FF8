using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100LoadedModelResourcesReader
{
    public static NativeBattleChoco100LoadedModelResources Read(byte[] executable, NativeBattleEffectFileAsset file)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffectFileAsset name = NativeBattleChoco100IoResourcesReader.ReadFileNames(executable).Single(item => item.FileId == 498);
        if (file.FileId != 498 || !string.Equals(file.Name, name.Name, StringComparison.OrdinalIgnoreCase) || file.Data.IsDefault || file.Data.Length != 0x5090)
        {
            throw new InvalidDataException("Native Choco100 loaded model requires the actual complete named file498.");
        }
        NativeBattleEffectOwnedModelResource model = ReadModel(file.Data);
        byte[] program = memory.Read(0x1529F64, 12);
        NativeBattleChoco100LoadedModelResources resources = new(model, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B7D8, 4)), new(0x1529F38, memory.Read(0x1529F38, 0x2C).ToImmutableArray()), new(model.Address + 0x5084, file.Data.Slice(0x5084, 12)), new(0x1529F64, Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadInt16LittleEndian(program.AsSpan(index * 2))).ToImmutableArray()));
        resources.Validate();
        return resources;
    }

    public static NativeBattleEffectOwnedModelResource ReadModel(ImmutableArray<byte> bytes)
    {
        int[] offsets = [0x18, 0x358, 0x4BCC, 0x5084, 0x5090];
        if (bytes.IsDefault || bytes.Length != 0x5090 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native Choco100 loaded model requires its actual four-section file extent.");
        }
        for (int index = 0; index < offsets.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != offsets[index])
            {
                throw new InvalidDataException("Native Choco100 loaded model section directory contradicts the original file bounds.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(offsets[0], offsets[1] - offsets[0]), bytes.Slice(offsets[2], offsets[3] - offsets[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(offsets[1], offsets[2] - offsets[1]).ToArray(), pose.BoneCount);
        if (pose.BoneCount != 17 || !NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()).SequenceEqual(new byte[] { 1, 28, 1 }) || geometry.Length != 1 || geometry[0].Vertices.Length != 508 || geometry[0].Polygons.Count(polygon => !polygon.Quad) != 912 || geometry[0].Polygons.Count(polygon => polygon.Quad) != 42)
        {
            throw new InvalidDataException("Native Choco100 loaded model contradicts its actual skeleton, poses or polygon counts.");
        }
        geometry[0].Validate();
        return new(0x19E8678, bytes, pose, geometry);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco100LoadedModelResources? nativeChoco100LoadedModel;

    public NativeBattleChoco100LoadedModelResources ReadNativeChoco100LoadedModelResources() => nativeChoco100LoadedModel ??= NativeBattleChoco100LoadedModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), ReadNativeChoco100IoResources().Files.Single(file => file.FileId == 498));
}
