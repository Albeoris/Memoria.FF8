using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLionHeart165ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadTextureNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new short[] { 540, 541, 542 }.Select(id =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + (uint)id * 4, 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException("Native Lion Heart texture filename is not terminated.");
            }
            return new NativeBattleEffectFileAsset(id, Encoding.ASCII.GetString(name.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleLionHeart165Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleLionHeart165ColdRegion> cold = new (uint Address, int Length)[] { (0x23C3278, 0x228), (0x23C34C0, 0x21C0), (0x23C5680, 0x290), (0x23C6730, 0x220) }.Select(range => new NativeBattleLionHeart165ColdRegion(range.Address, memory.ReadLoader(range.Address, range.Length).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD7AFE0, 0xD7B1C8, 0xD7B340, 0xD7B4D4 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD7B668, 0xD7D9A0, 0xD7F960, 0xD80230, 0xD813B0, 0xD81620, 0xD818A0, 0xD81B34, 0xD823E4, 0xD82B5C, 0xD83244, 0xD8392C, 0xD83E3C, 0xD84BE4, 0xD85D74, 0xD879EC }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleLionHeart165Resources result = new(new("FF8_EN:D780C8", memory.Read(NativeBattleLionHeart165Resources.StaticAddress, NativeBattleLionHeart165Resources.StaticLength).ToImmutableArray()), cold, files, sprites, meshes);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleLionHeart165Resources? nativeLionHeart165Resources;

    public NativeBattleLionHeart165Resources ReadNativeLionHeart165Resources()
    {
        if (nativeLionHeart165Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> names = NativeBattleLionHeart165ResourcesReader.ReadTextureNames(executable);
            using IFlArchive battle = OpenArchive("battle");
            nativeLionHeart165Resources = NativeBattleLionHeart165ResourcesReader.Read(executable, names.Select(name => name with { Data = Read(battle, name.Name).ToImmutableArray() }).ToImmutableArray());
        }
        return nativeLionHeart165Resources;
    }
}
