using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect318Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect318ResourcesReader
{
    public static string ReadFileName(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + 360 * 4, 4));
        byte[] bytes = memory.Read(address, 128);
        int end = bytes.AsSpan().IndexOf((byte)0);
        return end > 0 ? Encoding.ASCII.GetString(bytes.AsSpan(0, end)) : throw new InvalidDataException("Effect318 file input has no original directory name.");
    }

    public static NativeBattleEffect318Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, NativeBattleEffectFileAsset file360)
    {
        NativeExecutableMemory memory = new(executable);
        string fileName = ReadFileName(executable);
        if (file360.FileId != 360 || file360.Name != fileName)
        {
            throw new InvalidDataException("Effect318 completion file disagrees with its original directory.");
        }
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(CameraAddress, Bytes(CameraAddress, 596));
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ActAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ActLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleGfAnimationSource source in programmes.Values)
        {
            fronts.Add(source.Address, source.Data);
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleStaticMeshResource source in meshes.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        sprites = sprites.Add(ProjectedQuadsAddress, new(0, ProjectedQuadsAddress, 0, 0, Bytes(ProjectedQuadsAddress, ProjectedQuadsLength)));
        foreach (NativeBattleModuleResource source in sprites.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        NativeBattleEffect318Resources result = new(mainTim, new("effect318-camera", fronts[CameraAddress]), programmes, geometry, meshes, sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty.Add(360, file360), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect318Resources? nativeEffect318Resources;

    public NativeBattleEffect318Resources ReadNativeEffect318Resources()
    {
        if (nativeEffect318Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive magic = OpenArchive("magic");
            using IFlArchive battle = OpenArchive("battle");
            string fileName = NativeBattleEffect318ResourcesReader.ReadFileName(executable);
            NativeBattleEffectFileAsset file360 = new(360, fileName, Read(battle, fileName).ToImmutableArray());
            nativeEffect318Resources = NativeBattleEffect318ResourcesReader.Read(executable, new("mag317.tim", Read(magic, "mag317.tim").ToImmutableArray()), file360);
        }
        return nativeEffect318Resources;
    }
}
