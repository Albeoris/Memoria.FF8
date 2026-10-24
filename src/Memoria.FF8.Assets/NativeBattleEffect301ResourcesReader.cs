using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect301Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect301ResourcesReader
{
    public static NativeBattleEffect301Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(CameraAddress, Bytes(CameraAddress, 316));
        fronts.Add(0x103BBD8, Bytes(0x103BBD8, 294));
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
        NativeBattleEffect301Resources result = new(mainTim, new("effect301-camera", fronts[CameraAddress]), programmes, geometry, meshes, sprites, new("effect301-vibration", fronts[0x103BBD8]), Bytes(NativeBattleEffect301State.ScriptPointerAddress, 4), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect301Resources? nativeEffect301Resources;

    public NativeBattleEffect301Resources ReadNativeEffect301Resources()
    {
        if (nativeEffect301Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect301Resources = NativeBattleEffect301ResourcesReader.Read(executable, new("mag300.tim", Read(magic, "mag300.tim").ToImmutableArray()));
        }
        return nativeEffect301Resources;
    }
}
