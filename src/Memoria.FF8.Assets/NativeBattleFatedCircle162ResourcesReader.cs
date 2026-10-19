using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFatedCircle162ResourcesReader
{
    public static NativeBattleEffectFileAsset ReadTextureName(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + 4 * 537, 4));
        byte[] bytes = memory.Read(pointer, 128);
        int length = bytes.AsSpan().IndexOf((byte)0);
        if (length <= 0)
        {
            throw new InvalidDataException("Native Fated Circle texture filename is not terminated.");
        }
        return new(537, Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
    }

    public static NativeBattleFatedCircle162Resources Read(byte[] executable, NativeBattleEffectFileAsset textureFile)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleFatedCircle162ColdRegion> cold = new (uint Address, int Length)[] { (0x23CE608, 0x18), (0x23CE650, 0x3D0), (0x23CEA40, 0x9F0), (0x23D0254, 0x548), (0x23D079C, 0x5F0) }.Select(range => new NativeBattleFatedCircle162ColdRegion(range.Address, memory.ReadLoader(range.Address, range.Length).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xDA19B4, 0xDA1B60, 0xDA1D74, 0xDA1F20, 0xDA219C }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        uint[] ordinaryMeshes = [0xDA2348, 0xDA3A58, 0xDA3E48, 0xDA4118, 0xDA4308, 0xDA4488, 0xDA6700, 0xDA7198];
        IEnumerable<uint> fragments = Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDA8C74u + (uint)index * 4, 4)));
        ImmutableArray<NativeBattleStaticMeshResource> meshes = ordinaryMeshes.Concat(fragments).Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleFatedCircle162Resources result = new(new("FF8_EN:DA0100", memory.Read(NativeBattleFatedCircle162Resources.StaticAddress, NativeBattleFatedCircle162Resources.StaticLength).ToImmutableArray()), textureFile, cold, sprites, meshes);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFatedCircle162Resources? nativeFatedCircle162Resources;

    public NativeBattleFatedCircle162Resources ReadNativeFatedCircle162Resources()
    {
        if (nativeFatedCircle162Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            NativeBattleEffectFileAsset name = NativeBattleFatedCircle162ResourcesReader.ReadTextureName(executable);
            using IFlArchive battle = OpenArchive("battle");
            nativeFatedCircle162Resources = NativeBattleFatedCircle162ResourcesReader.Read(executable, name with { Data = Read(battle, name.Name).ToImmutableArray() });
        }
        return nativeFatedCircle162Resources;
    }
}
