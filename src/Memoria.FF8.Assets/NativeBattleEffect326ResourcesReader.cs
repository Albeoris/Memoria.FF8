using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect326ResourcesReader
{
    public static NativeBattleEffect326Resources Read(IGameNativeMemorySource memory, Func<short, string, ImmutableArray<byte>> readBattleFile)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(readBattleFile);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(8);
        for (short id = 0x2E1; id <= 0x2E8; id++)
        {
            uint address = Dword(checked(0xB84CCCu + (uint)id * 4));
            byte[] name = memory.Read(address, 128);
            int end = name.AsSpan().IndexOf((byte)0);
            if (end <= 0)
            {
                throw new InvalidDataException("Effect326 file directory lacks its original terminated name.");
            }
            string fileName = Encoding.ASCII.GetString(name.AsSpan(0, end));
            files.Add(new(id, fileName, readBattleFile(id, fileName)));
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address));
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.ToImmutableDictionary(address => address, address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address)));
        ImmutableArray<NativeBattleEffectFileAsset> loadedFiles = files.MoveToImmutable();
        NativeBattleEffect326Resources result = new(new("effect187-texture", Bytes(TextureAddress, TextureLength)), new("effect187-camera", Bytes(CameraAddress, CameraLength)), new("effect187-vibration", Bytes(VibrationAddress, VibrationLength)), loadedFiles, meshes, sprites, Enumerable.Range(0, 6).Select(index => Dword(SoundAddress + (uint)(index * 4))).ToImmutableArray(), Dword(StreamAddress))
        {
            MainModel = ReadModel(ArenaAddress, loadedFiles[1].Data),
            EmbeddedModel = ReadModel(EmbeddedModelAddress, Bytes(EmbeddedModelAddress, EmbeddedModelLength)),
            LateModel = ReadModel(PixelSource, loadedFiles[7].Data),
            ScrollPixels = Bytes(PixelSource, 0x4000),
            StaticFronts = StaticRanges.ToImmutableDictionary(source => source.Address, source => Bytes(source.Address, source.Length))
        };
        result.Validate();
        return result;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }

    private static NativeBattleEffectOwnedModelResource ReadModel(uint address, ImmutableArray<byte> bytes)
    {
        int skeleton = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int geometry = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        int end = NativeBattleDatDirectory.Section(bytes.AsSpan(), 3);
        NativeBattlePoseSource pose = new(bytes.Slice(skeleton, geometry - skeleton), bytes.Slice(animations, end - animations));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(bytes.Slice(geometry, animations - geometry).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in meshes)
        {
            mesh.Validate();
        }
        return new(address, bytes, pose, meshes);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect326Resources? nativeEffect326Resources;

    public NativeBattleEffect326Resources ReadNativeEffect326Resources()
    {
        if (nativeEffect326Resources is null)
        {
            using IFlArchive battle = OpenArchive("battle");
            nativeEffect326Resources = NativeBattleEffect326ResourcesReader.Read(ReadNativeMemory(), (_, name) => Read(battle, name).ToImmutableArray());
        }
        return nativeEffect326Resources;
    }
}
