using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect338ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 362 });

    public static NativeBattleEffect338Resources Read(byte[] executable, NativeBattleEffectFileAsset completionFile)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffectFileAsset expected = ReadFileNames(executable).Single();
        if (completionFile.FileId != expected.FileId || !string.Equals(completionFile.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Effect338 requires its original selected completion file.");
        }
        ImmutableArray<byte> modelBytes = Bytes(0x136BB30, 0x4DD0);
        NativeBattlePoseSource pose = new(modelBytes.Slice(0x18, 0x550), modelBytes.Slice(0x3730, 0x4DC4 - 0x3730));
        pose.Validate();
        NativeBattleEffectOwnedModelResource model = new(0x136BB30, modelBytes, pose, NativeBattleRawGeometryReader.Read(modelBytes.Slice(0x568, 0x3730 - 0x568).ToArray(), pose.BoneCount));
        ImmutableArray<NativeBattleEffectHeapAllocation> textures = new[] { (0x1376698u, 16928), (0x137A8B8u, 135200), (0x139B8D8u, 16928) }.Select(row => new NativeBattleEffectHeapAllocation($"effect338-{row.Item1:X}", Bytes(row.Item1, row.Item2))).ToImmutableArray();
        ImmutableArray<byte> vibration = Bytes(0x13710CC, 304);
        NativeVibrationBank bank = new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration);
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = new[] { (0x1372E60u, 5036), (0x137420Cu, 5036), (0x13755B8u, 4252) }.Select(row => new NativeBattleGfAnimationSource(row.Item1, Bytes(row.Item1, row.Item2))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = new uint[] { 0x1371640, 0x1372CE8 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableDictionary(mesh => mesh.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = new uint[] { 0x137130C, 0x1371464, 0x13711FC }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableDictionary(sprite => sprite.Address);
        NativeBattleEffect338Resources result = new(model, textures, new("effect338-camera", Bytes(0x1370900, 604)), bank, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x13710C8, 4)), programmes, meshes, sprites, Bytes(0x1376658, 64), completionFile);
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect338Resources? nativeEffect338Resources;

    public NativeBattleEffect338Resources ReadNativeEffect338Resources()
    {
        if (nativeEffect338Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleEffectFileAsset file = NativeBattleEffect338ResourcesReader.ReadFileNames(executable).Single();
            nativeEffect338Resources = NativeBattleEffect338ResourcesReader.Read(executable, file with { Data = Read(battle, file.Name).ToImmutableArray() });
        }
        return nativeEffect338Resources;
    }
}
