using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect174ResourcesReader
{
    public static NativeBattleEffect174Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag173.tim\0")
        {
            throw new InvalidDataException("Effect174 initializer texture name changed.");
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
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        NativeBattleEffect174Resources result = new(mainTim, new("effect174-camera", fronts[CameraAddress]), meshes, sprites, Table(HistoryColorsAddress, 5), Table(MaterialCycleAddress, 12), U32(SoundAddress), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<uint> Table(uint address, int count) => Enumerable.Range(0, count).Select(index => U32(address + (uint)index * 4)).ToImmutableArray();
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect174Resources? nativeEffect174Resources;

    public NativeBattleEffect174Resources ReadNativeEffect174Resources()
    {
        if (nativeEffect174Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect174Resources = NativeBattleEffect174ResourcesReader.Read(ReadNativeExecutable(), new("mag173.tim", Read(magic, "mag173.tim").ToImmutableArray()));
        }
        return nativeEffect174Resources;
    }
}
