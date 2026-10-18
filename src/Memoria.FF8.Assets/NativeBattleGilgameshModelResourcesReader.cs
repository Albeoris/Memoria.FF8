using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshModelResourcesReader
{
    public static NativeBattleGilgameshModelResources Read(byte[] executable, NativeBattleGilgameshResources files, NativeBattleEffectHeapAllocation initializerTim)
    {
        files.Validate();
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectOwnedModelResource>.Builder staticModels = ImmutableArray.CreateBuilder<NativeBattleEffectOwnedModelResource>(4);
        uint[] addresses = [0xE808D0, 0xE81934, 0xE82808, 0xE839F4];
        int[] lengths = [0x1064, 0xED4, 0x11EC, 0x11EC];
        for (int index = 0; index < addresses.Length; index++)
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xCD0418u + (uint)index * 4), 4));
            if (selected != addresses[index])
            {
                throw new InvalidDataException("Native Excalipoor model pointer table contradicts its selected variant three sources.");
            }
            staticModels.Add(ReadModel(selected, memory.Read(selected, lengths[index]).ToImmutableArray()));
        }
        ImmutableArray<byte> stream749 = files.Files.Single(file => file.FileId == 749).Bytes;
        int[] offsets = [0, 6044, 12284, 18524, 24764];
        ImmutableArray<NativeBattleEffectOwnedModelResource> submodels = offsets.Select((offset, index) => ReadModel(checked(0x2101AB8u + (uint)offset), stream749.Slice(offset, index == 0 ? 6044 : 6240))).ToImmutableArray();
        NativeBattleGilgameshModelResources result = new(initializerTim, ReadModel(0x20DFAB8, files.Files.Single(file => file.FileId == 746).Bytes), ReadModel(0x20DFAB8, files.Files.Single(file => file.FileId == 751).Bytes), submodels, staticModels.MoveToImmutable());
        result.Validate();
        return result;
    }

    private static NativeBattleEffectOwnedModelResource ReadModel(uint address, ImmutableArray<byte> bytes)
    {
        int skeleton = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int geometryOffset = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        int end = NativeBattleDatDirectory.Section(bytes.AsSpan(), 3);
        NativeBattlePoseSource pose = new(bytes.Slice(skeleton, geometryOffset - skeleton), bytes.Slice(animations, end - animations));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(geometryOffset, animations - geometryOffset).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        return new(address, bytes, pose, geometry);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshModelResources? nativeGilgameshModelResources;

    public NativeBattleGilgameshModelResources ReadNativeGilgameshModelResources()
    {
        if (nativeGilgameshModelResources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectHeapAllocation initializer = new("mag326-329.tim", Read(magic, "mag326-329.tim").ToImmutableArray());
            nativeGilgameshModelResources = NativeBattleGilgameshModelResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), ReadNativeGilgameshResources(), initializer);
        }
        return nativeGilgameshModelResources;
    }
}
