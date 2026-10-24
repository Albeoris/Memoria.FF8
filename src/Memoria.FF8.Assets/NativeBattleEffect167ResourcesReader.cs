using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect167ResourcesReader
{
    public static NativeBattleEffect167Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag166.tim\0")
        {
            throw new InvalidDataException("Effect167 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in StaticRanges)
        {
            fronts.Add(address, Bytes(address, length));
        }
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values)
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        NativeBattleModuleResource sprite = new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, SpriteLength));
        fronts.Add(SpriteAddress, sprite.Bytes);
        NativeBattleEffect167Resources result = new(mainTim, new("effect167-camera", fronts[CameraAddress]), meshes, sprite, U32(SoundAddress), Enumerable.Range(0, 56).Select(index => U32(MappingAddress + (uint)index * 4)).ToImmutableArray(), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect167Resources? nativeEffect167Resources;

    public NativeBattleEffect167Resources ReadNativeEffect167Resources()
    {
        if (nativeEffect167Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect167Resources = NativeBattleEffect167ResourcesReader.Read(ReadNativeExecutable(), new("mag166.tim", Read(magic, "mag166.tim").ToImmutableArray()));
        }
        return nativeEffect167Resources;
    }
}
