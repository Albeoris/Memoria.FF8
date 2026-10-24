using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect213Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect213ResourcesReader
{
    public static NativeBattleEffect213Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(CameraAddress, Bytes(CameraAddress, 392));
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
        foreach (NativeBattleModuleResource source in sprites.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        NativeBattleEffect213Resources result = new(mainTim, new("effect213-camera", fronts[CameraAddress]), programmes, geometry, meshes, sprites, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect213Resources? nativeEffect213Resources;

    public NativeBattleEffect213Resources ReadNativeEffect213Resources()
    {
        if (nativeEffect213Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect213Resources = NativeBattleEffect213ResourcesReader.Read(ReadNativeExecutable(), new("mag212.tim", Read(magic, "mag212.tim").ToImmutableArray()));
        }
        return nativeEffect213Resources;
    }
}
