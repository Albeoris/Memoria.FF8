using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRayBomb10ResourcesReader
{
    public static NativeBattleRayBomb10Resources Read(byte[] executable)
    {
        ushort cameraLength = BinaryPrimitives.ReadUInt16LittleEndian(Read(0xDEA5CE, 2).AsSpan());
        if (cameraLength != 0x270)
        {
            throw new InvalidDataException("Native627E60 camera programme does not end at its original sprite directory.");
        }
        NativeBattleEffectHeapAllocation camera = new("FF8_EN:DEA5C8", Read(0xDEA5C8, cameraLength));
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xDEAD64, 0xDED00C, 0xDEC964, 0xDEC13C, 0xDEC3F4 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(executable, address)).ToImmutableArray();
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0xDEABB8, 0xDEA9E4, 0xDEAA0C, 0xDEA838 }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, address))).ToImmutableArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(Read(0xDED580, 4).AsSpan()) != 0xDEC13C || BinaryPrimitives.ReadUInt32LittleEndian(Read(0xDED584, 4).AsSpan()) != 0xDEC3F4)
        {
            throw new InvalidDataException("Native Ray-Bomb debris directory differs from its original two meshes.");
        }
        return new(camera, meshes, sprites, Read(0xDED590, 48), Read(0xDED5C0, 48), Read(0xDED530, 20), Read(0xDED558, 20), Read(0xDED544, 20), Read(0xDED56C, 20), BinaryPrimitives.ReadUInt32LittleEndian(Read(0xDED524, 4).AsSpan()), BinaryPrimitives.ReadUInt32LittleEndian(Read(0xDED528, 4).AsSpan()));
        ImmutableArray<byte> Read(uint address, int count) => NativeBattleEffectStaticResourcesReader.Read(executable, address, count);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRayBomb10Resources? nativeRayBomb10Resources;
    public NativeBattleRayBomb10Resources ReadNativeRayBomb10Resources() => nativeRayBomb10Resources ??= NativeBattleRayBomb10ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
