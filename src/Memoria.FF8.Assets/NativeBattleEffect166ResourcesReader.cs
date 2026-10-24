using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect166ResourcesReader
{
    public static NativeBattleEffect166Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag311.tim\0")
        {
            throw new InvalidDataException("Effect166 initializer texture name changed.");
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
        NativeBattleEffect166Resources result = new(mainTim, new("effect166-camera", fronts[CameraAddress]), meshes, sprite, Enumerable.Range(0, 12).Select(index => U32(PaletteAddress + (uint)index * 4)).ToImmutableArray(), Enumerable.Range(0, 3).Select(index => U32(MeshTableAddress + (uint)index * 4)).ToImmutableArray(), Enumerable.Range(0, 2).Select(index => U32(0xD7803Cu + (uint)index * 4)).ToImmutableArray(), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect166Resources? nativeEffect166Resources;

    public NativeBattleEffect166Resources ReadNativeEffect166Resources()
    {
        if (nativeEffect166Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect166Resources = NativeBattleEffect166ResourcesReader.Read(ReadNativeExecutable(), new("mag311.tim", Read(magic, "mag311.tim").ToImmutableArray()));
        }
        return nativeEffect166Resources;
    }
}
