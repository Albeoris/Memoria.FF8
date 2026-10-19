using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBerserk108ResourcesReader
{
    public static NativeBattleBerserk108Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x12E6DDC, 0x12E6E98, 0x12E6C48 }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        return new(new(0x12E7BC0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x12E7BC0, 0x3E14)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x12E7010u).Bytes, sprites, Enumerable.Range(0, 7).Select(index => U32(0x12EC330u + (uint)index * 4)).ToImmutableArray(), Enumerable.Range(0, 4).Select(index => U32(0x12EC320u + (uint)index * 4)).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.Read(memory, 0x12EC310, 16), new("FF8_EN:12EB9D4", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12EB9D4, 0x938)), U32(0x12EC30C));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBerserk108Resources? nativeBerserk108Resources;
    public NativeBattleBerserk108Resources ReadNativeBerserk108Resources() => nativeBerserk108Resources ??= NativeBattleBerserk108ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
