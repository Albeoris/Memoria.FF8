using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect187ResourcesReader
{
    public static NativeBattleEffect187Resources Read(IGameNativeMemorySource memory, Func<short, string, ImmutableArray<byte>> readBattleFile)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(readBattleFile);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(6);
        for (short id = 0x225; id <= 0x22A; id++)
        {
            uint address = Dword(checked(0xB84CCCu + (uint)id * 4));
            byte[] name = memory.Read(address, 128);
            int end = name.AsSpan().IndexOf((byte)0);
            if (end <= 0)
            {
                throw new InvalidDataException("Effect187 file directory lacks its original terminated name.");
            }
            string fileName = Encoding.ASCII.GetString(name.AsSpan(0, end));
            files.Add(new(id, fileName, readBattleFile(id, fileName)));
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address));
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.ToImmutableDictionary(address => address, address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address)));
        ImmutableArray<NativeBattleEffectFileAsset> loadedFiles = files.MoveToImmutable();
        NativeBattleEffect187Resources result = new(new("effect187-texture", Bytes(TextureAddress, TextureLength)), new("effect187-camera", Bytes(CameraAddress, CameraLength)), new("effect187-vibration", Bytes(VibrationAddress, VibrationLength)), loadedFiles, meshes, sprites, [Dword(SoundAddress), Dword(SoundAddress + 4), Dword(SoundAddress + 8)], Dword(StreamAddress))
        {
            OwnedModel = ReadModel(loadedFiles[1].Data),
            ScrollPixels = Bytes(0xFA54A8, 0x4000),
            ScrollRectangle = Bytes(0xE41EA0, 8),
            StaticFronts = new (uint Address, int Length)[] { (0xE41E5C, 28), (0xE41E90, 8), (0xE41EA8, 12), (0xE41EF8, 48), (0xE4206C, 224), (0xE3FD70, 1008) }.ToImmutableDictionary(source => source.Address, source => Bytes(source.Address, source.Length))
        };
        result.Validate();
        return result;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }

    private static NativeBattleEffectOwnedModelResource ReadModel(ImmutableArray<byte> bytes)
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
        return new(NativeBattleEffect187Profile.ArenaAddress, bytes, pose, meshes);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect187Resources? nativeEffect187Resources;

    public NativeBattleEffect187Resources ReadNativeEffect187Resources()
    {
        if (nativeEffect187Resources is null)
        {
            using IFlArchive battle = OpenArchive("battle");
            nativeEffect187Resources = NativeBattleEffect187ResourcesReader.Read(ReadNativeMemory(), (_, name) => Read(battle, name).ToImmutableArray());
        }
        return nativeEffect187Resources;
    }
}
