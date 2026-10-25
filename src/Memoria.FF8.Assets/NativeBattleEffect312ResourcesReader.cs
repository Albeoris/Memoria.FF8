using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect312ResourcesReader
{
    public static NativeBattleEffect312Resources Read(byte[] executable, NativeBattleEffectHeapAllocation mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, InitializerTextureName.Length + 1)) != InitializerTextureName + "\0")
        {
            throw new InvalidDataException("Effect312 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in StaticRanges)
        {
            fronts.Add(address, Bytes(address, length));
        }
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values)
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        NativeBattleEffect312Resources result = new(mainTim, new("effect312-camera", fronts[CameraAddress]), meshes, sprites, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect312Resources? nativeEffect312Resources;

    public NativeBattleEffect312Resources ReadNativeEffect312Resources()
    {
        if (nativeEffect312Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect312Resources = NativeBattleEffect312ResourcesReader.Read(ReadNativeExecutable(), new(InitializerTextureName, Read(magic, InitializerTextureName).ToImmutableArray()));
        }
        return nativeEffect312Resources;
    }
}
