using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect66ResourcesReader
{
    public static NativeBattleEffect66Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag065.tim\0")
        {
            throw new InvalidDataException("Effect66 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(mesh => mesh.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(sprite => sprite.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in new (uint, int)[] { (VibrationAddress, VibrationLength), (CameraAddress, CameraLength), (EmbeddedTimAddress, EmbeddedTimLength), (0xDEA344, 24), (0xDEA360, 116), (0xDEA3DC, 72), (0xDEA424, 48), (0xDEA454, 48) })
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
        NativeBattleEffect66Resources result = new(mainTim, new("FF8_EN:ECC40C", fronts[EmbeddedTimAddress]), new("FF8_EN:DEA484", fronts[CameraAddress]), new("FF8_EN:DEA20C", fronts[VibrationAddress]), meshes, sprites, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect66Resources? nativeEffect66Resources;

    public NativeBattleEffect66Resources ReadNativeEffect66Resources()
    {
        if (nativeEffect66Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect66Resources = NativeBattleEffect66ResourcesReader.Read(ReadNativeExecutable(), new("mag065.tim", Read(magic, "mag065.tim").ToImmutableArray()));
        }
        return nativeEffect66Resources;
    }
}
