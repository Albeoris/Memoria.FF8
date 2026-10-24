using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect68ResourcesReader
{
    public static NativeBattleEffect68Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 15)) != "mag039-067.tim\0")
        {
            throw new InvalidDataException("Effect68 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(mesh => mesh.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(sprite => sprite.Address);
        ImmutableDictionary<uint, ImmutableArray<byte>> vertices = VertexBankAddresses.Select((address, index) => new KeyValuePair<uint, ImmutableArray<byte>>(address, Bytes(address, VertexBankLengths[index]))).ToImmutableDictionary();
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in StaticRanges)
        {
            fronts.Add(address, Bytes(address, length));
        }
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values.Where(mesh => !MutableMeshAddresses.Contains(mesh.Address)))
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        foreach ((uint address, ImmutableArray<byte> source) in vertices)
        {
            fronts.Add(address, source);
        }
        NativeBattleEffect68Resources result = new(mainTim, BinaryPrimitives.ReadUInt32LittleEndian(fronts[SoundAddress].AsSpan()), meshes, sprites, vertices, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect68Resources? nativeEffect68Resources;

    public NativeBattleEffect68Resources ReadNativeEffect68Resources()
    {
        if (nativeEffect68Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect68Resources = NativeBattleEffect68ResourcesReader.Read(ReadNativeExecutable(), new("mag039-067.tim", Read(magic, "mag039-067.tim").ToImmutableArray()));
        }
        return nativeEffect68Resources;
    }
}
