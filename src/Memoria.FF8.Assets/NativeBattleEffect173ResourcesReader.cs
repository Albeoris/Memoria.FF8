using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect173ResourcesReader
{
    public static NativeBattleEffect173Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag172.tim\0")
        {
            throw new InvalidDataException("Effect173 initializer texture name changed.");
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
        NativeBattleGfAnimationSource programme = new(ProgrammeAddress, Bytes(ProgrammeAddress, ProgrammeLength));
        fronts.Add(ProgrammeAddress, programme.Data);
        NativeBattleEffect173Resources result = new(mainTim, new("effect173-camera", fronts[CameraAddress]), meshes, sprites, programme, Table(PaletteAddress, 16), Table(MeshSourcesAddress, 5), Table(FaceRecordDirectoryAddress, 5), U32(ProgrammeFlagsAddress), Table(SoundAddress, 2), Bytes(BirthRowsAddress, 32), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<uint> Table(uint address, int count) => Enumerable.Range(0, count).Select(index => U32(address + (uint)index * 4)).ToImmutableArray();
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect173Resources? nativeEffect173Resources;

    public NativeBattleEffect173Resources ReadNativeEffect173Resources()
    {
        if (nativeEffect173Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect173Resources = NativeBattleEffect173ResourcesReader.Read(ReadNativeExecutable(), new("mag172.tim", Read(magic, "mag172.tim").ToImmutableArray()));
        }
        return nativeEffect173Resources;
    }
}
