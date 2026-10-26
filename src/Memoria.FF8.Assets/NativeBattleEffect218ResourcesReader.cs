using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect218ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 705, 706, 707, 708, 709 });

    public static NativeBattleEffect218Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> expected = ReadFileNames(executable);
        if (files.IsDefault || files.Length != expected.Length)
        {
            throw new InvalidDataException("Effect218 requires its five original streamed files.");
        }
        for (int index = 0; index < expected.Length; index++)
        {
            if (files[index].FileId != expected[index].FileId || !string.Equals(files[index].Name, expected[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Effect218 streamed file differs from its original directory selection.");
            }
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Builder meshes = ImmutableDictionary.CreateBuilder<uint, NativeBattleStaticMeshResource>();
        foreach ((uint address, int length) in new (uint, int)[] { (0xE1F6A8u, 304), (0xE1F7D8u, 176), (0xE1F888u, 176), (0xE1F938u, 296), (0xE1FA60u, 232), (0xE1FB48u, 296), (0xE1FC70u, 544), (0xE1FE90u, 304), (0xE1FFC0u, 424), (0xE20168u, 608), (0xE203C8u, 608), (0xE20628u, 608), (0xE20888u, 664), (0xE20B20u, 360), (0xE20C88u, 296), (0xE20DB0u, 536), (0xE20FC8u, 664), (0xE21260u, 232), (0xE21348u, 240), (0xE21438u, 488), (0xE21620u, 544), (0xE21840u, 488), (0xE21A28u, 176), (0xE21AD8u, 488), (0xE21CC0u, 2224), (0xE1B7E0u, 4408), (0xE22570u, 4336), (0xE23660u, 6504), (0xE254D8u, 448), (0xE1A0F0u, 5872), (0xE1EF08u, 1768), (0xE1C918u, 6472), (0xE1E260u, 3240), (0xE1F5F0u, 184), (0xE24FC8u, 1296) })
        {
            NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
            if (mesh.Bytes.Length != length)
            {
                throw new InvalidDataException("Effect218 mesh leaves its original counted geometry extent.");
            }
            meshes.Add(address, mesh);
        }
        ImmutableDictionary<uint, NativeBattleModuleResource>.Builder sprites = ImmutableDictionary.CreateBuilder<uint, NativeBattleModuleResource>();
        foreach ((uint address, int length) in new (uint, int)[] { (0xE19C14, 168), (0xE19CBC, 428), (0xE19E68, 428) })
        {
            ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address);
            if (bytes.Length != length)
            {
                throw new InvalidDataException("Effect218 sprite leaves its original complete frame directory.");
            }
            sprites.Add(address, new(0, address, 0, 0, bytes));
        }
        ImmutableArray<byte> vibration = Bytes(0xE25698, 312);
        NativeVibrationBank bank = new(memory.SourceHash, [0, 256], vibration);
        ImmutableArray<byte> clearQuad = Bytes(0xE25AD8, 24);
        NativeBattleEffect218Resources result = new(new("effect218-initial-texture", Bytes(0xF1E920, 166432)), files, Model(0x20DFAB8, files[1].Data), Model(0xF1DD54, Bytes(0xF1DD54, 0xBCC)), meshes.ToImmutable(), sprites.ToImmutable(), Enumerable.Range(0, 24).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xE25850u + (uint)index * 4, 4))).ToImmutableArray(), Bytes(0xE257F0, 64), Bytes(0xE25830, 16), Bytes(0xE25840, 16), Bytes(0xE258B0, 192), clearQuad, new("effect218-camera", Bytes(0xE25970, 354)), bank, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xF1DD50, 4)), Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xE257D0u + (uint)index * 4, 4))).ToImmutableArray());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }

    private static NativeBattleEffectOwnedModelResource Model(uint address, ImmutableArray<byte> bytes)
    {
        int skeleton = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int geometry = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        int end = NativeBattleDatDirectory.Section(bytes.AsSpan(), 3);
        NativeBattlePoseSource pose = new(bytes.Slice(skeleton, geometry - skeleton), bytes.Slice(animations, end - animations));
        pose.Validate();
        return new(address, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(geometry, animations - geometry).ToArray(), pose.BoneCount));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect218Resources? nativeEffect218Resources;

    public NativeBattleEffect218Resources ReadNativeEffect218Resources()
    {
        if (nativeEffect218Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect218ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeEffect218Resources = NativeBattleEffect218ResourcesReader.Read(executable, files);
        }
        return nativeEffect218Resources;
    }
}
